using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using PixConvert.Models;

namespace PixConvert.Services;

/// <summary>
/// IFileAnalyzerService의 상세 구현 클래스입니다.
/// </summary>
public class FileAnalyzerService : IFileAnalyzerService
{
    private readonly record struct FileItemBatchResult(List<FileItem> Items, int DuplicateCount, int IgnoredCount, int FailedCount);

    /// <summary>
    /// 직접 파일 처리와 폴더 스캔 결과를 한 번에 반영할 수 있도록 누적합니다.
    /// </summary>
    private sealed class FileProcessingAccumulator
    {
        public List<FileItem> Items { get; } = new();
        public int DuplicateCount { get; private set; }
        public int IgnoredCount { get; private set; }
        public int FailedCount { get; private set; }

        /// <summary>
        /// 배치 결과 하나를 현재 누적 합계에 더합니다.
        /// </summary>
        public void Add(FileItemBatchResult result)
        {
            // 직접 파일 처리와 폴더 스캔 결과를 같은 방식으로 합산한다.
            Items.AddRange(result.Items);
            DuplicateCount += result.DuplicateCount;
            IgnoredCount += result.IgnoredCount;
            FailedCount += result.FailedCount;
        }

        /// <summary>
        /// 누적된 합계를 공개 처리 결과 객체에 반영합니다.
        /// </summary>
        public void ApplyTo(FileProcessingResult result)
        {
            result.NewItems = Items;
            result.DuplicateCount += DuplicateCount;
            result.IgnoredCount += IgnoredCount;
            result.FailedCount += FailedCount;
        }
    }

    /// <summary>
    /// 대량 스캔 중 UI 스레드에 과도한 진행률 갱신이 쌓이지 않도록 조절합니다.
    /// </summary>
    private sealed class BatchProgressTracker
    {
        private readonly IProgress<FileProcessingProgress>? _progress;
        private readonly int _baseOffset;
        private readonly int _totalCount;
        private int _processedCount;

        /// <summary>
        /// 파일 아이템 생성 배치 하나를 위한 진행률 추적기를 만듭니다.
        /// </summary>
        public BatchProgressTracker(IProgress<FileProcessingProgress>? progress, int baseOffset, int totalCount)
        {
            _progress = progress;
            _baseOffset = baseOffset;
            _totalCount = totalCount;
        }

        /// <summary>
        /// 처리 개수를 증가시키고 제한된 주기로 진행률을 보고합니다.
        /// </summary>
        public void ReportProcessed()
        {
            // 진행률 UI 갱신 빈도를 제한해 대량 스캔 시 메시지 갱신 비용을 줄인다.
            int current = Interlocked.Increment(ref _processedCount);
            if (_progress != null && (current % 100 == 0 || current == _totalCount))
            {
                _progress.Report(new FileProcessingProgress
                {
                    CurrentIndex = _baseOffset + current,
                    TotalCount = _baseOffset + _totalCount
                });
            }
        }
    }

    private readonly IFileScannerService _fileScannerService;
    private readonly ILogger<FileAnalyzerService> _logger;
    private readonly ILanguageService _languageService;
    private readonly IDriveInfoService _driveInfoService;

    /// <summary>
    /// 스캔, 로깅, 지역화, 병렬도 계산에 필요한 서비스를 받아 분석기를 생성합니다.
    /// </summary>
    public FileAnalyzerService(
        IFileScannerService fileScannerService,
        ILogger<FileAnalyzerService> logger,
        ILanguageService languageService,
        IDriveInfoService driveInfoService)
    {
        _fileScannerService = fileScannerService;
        _logger = logger;
        _languageService = languageService;
        _driveInfoService = driveInfoService;
    }

    /// <summary>
    /// 분석기 로그와 메시지에 사용할 지역화 문자열을 가져옵니다.
    /// </summary>
    private string GetString(string key) => _languageService.GetString(key);

    /// <inheritdoc/>
    public async Task<FileProcessingResult> ProcessPathsAsync(
        IEnumerable<string> paths,
        int maxItemCount,
        int currentCount,
        IReadOnlySet<string>? existingPaths = null,
        IProgress<FileProcessingProgress>? progress = null)
    {
        var result = new FileProcessingResult();
        var sw = Stopwatch.StartNew();
        var rawPaths = paths.ToList();
        result.TotalPathCount = rawPaths.Count;

        var seenPaths = existingPaths != null
            ? new HashSet<string>(existingPaths, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int fileCapacity = maxItemCount - currentCount;
        var accumulator = new FileProcessingAccumulator();

        if (fileCapacity <= 0)
        {
            result.IgnoredCount = rawPaths.Count;
            _logger.LogWarning(GetString("Log_Process_LimitReached"), maxItemCount, rawPaths.Count);
            return result;
        }

        var folders = rawPaths.Where(Directory.Exists).ToList();
        var directFiles = rawPaths.Where(p => !Directory.Exists(p)).ToList();

        // 1단계: 직접 선택된 파일 처리
        if (directFiles.Count > 0)
        {
            var directFileResult = await ProcessDirectFilesAsync(
                directFiles, seenPaths, fileCapacity, progress, accumulator.Items.Count);

            accumulator.Add(directFileResult);
            fileCapacity -= directFileResult.Items.Count;
        }

        // 2단계: 폴더 스캔 처리
        if (folders.Count > 0)
        {
            var folderResult = await ProcessFoldersAsync(
                folders, seenPaths, fileCapacity, progress, accumulator.Items.Count);

            accumulator.Add(folderResult);
        }

        accumulator.ApplyTo(result);
        sw.Stop();

        _logger.LogInformation(GetString("Log_Process_Summary"),
            result.TotalPathCount, result.SuccessCount,
            result.DuplicateCount, result.IgnoredCount, result.FailedCount, sw.ElapsedMilliseconds);

        return result;
    }

    /// <summary>
    /// 직접 선택된 파일 경로 목록에서 중복을 제거하고 FileItem 배치를 생성합니다.
    /// </summary>
    private async Task<FileItemBatchResult>
        ProcessDirectFilesAsync(
            List<string> filePaths,
            HashSet<string> seenPaths,
            int fileCapacity,
            IProgress<FileProcessingProgress>? progress,
            int alreadyProcessedCount)
    {
        var unique = new List<string>();
        int duplicateCount = 0;

        foreach (var path in filePaths)
        {
            if (!seenPaths.Add(path))
            {
                duplicateCount++;
                continue;
            }

            unique.Add(path);
        }

        int canAdd = Math.Min(unique.Count, fileCapacity);
        int ignoredCount = unique.Count - canAdd;

        if (canAdd == 0)
            return new FileItemBatchResult(new List<FileItem>(), duplicateCount, ignoredCount, 0);

        var targets = unique.Take(canAdd).ToList();
        var (items, failedCount) = await CreateItemsBatchAsync(targets, progress, alreadyProcessedCount);

        return new FileItemBatchResult(items, duplicateCount, ignoredCount, failedCount);
    }

    /// <summary>
    /// 폴더 경로 목록을 재귀 스캔하여 중복·용량 한도 처리 후 FileItem 배치를 생성합니다.
    /// </summary>
    private async Task<FileItemBatchResult>
        ProcessFoldersAsync(
            List<string> folders,
            HashSet<string> seenPaths,
            int fileCapacity,
            IProgress<FileProcessingProgress>? progress,
            int alreadyProcessedCount)
    {
        if (fileCapacity <= 0)
            return new FileItemBatchResult(new List<FileItem>(), 0, folders.Count, 0);

        var folderFiles = new List<FileInfo>();
        int duplicateCount = 0;
        int ignoredCount = 0;

        await Task.Run(() =>
        {
            foreach (var folderPath in folders)
            {
                if (fileCapacity <= 0) break;

                // yield return 반복자를 통한 지연 로딩 스캔 (메모리 효율화)
                foreach (var file in _fileScannerService.GetFilesInFolder(folderPath))
                {
                    if (!seenPaths.Add(file.FullName))
                    {
                        duplicateCount++;
                        continue;
                    }

                    if (fileCapacity > 0)
                    {
                        folderFiles.Add(file);
                        fileCapacity--;
                    }
                    else
                    {
                        ignoredCount++;
                    }
                }

                if (fileCapacity <= 0)
                    _logger.LogWarning(GetString("Log_Process_LimitReached"), "∞", "Scanning...");
            }
        });

        if (folderFiles.Count == 0)
            return new FileItemBatchResult(new List<FileItem>(), duplicateCount, ignoredCount, 0);

        var paths = folderFiles.Select(f => f.FullName).ToList();
        var (items, failedCount) = await CreateItemsBatchAsync(paths, progress, alreadyProcessedCount);

        return new FileItemBatchResult(items, duplicateCount, ignoredCount, failedCount);
    }

    /// <summary>
    /// 파일 경로 목록을 Parallel.ForEachAsync로 병렬 처리하여 FileItem 컬렉션을 반환합니다.
    /// 병렬도는 IDriveInfoService에 위임합니다. 진행률 보고를 포함합니다.
    /// </summary>
    private async Task<(List<FileItem> Items, int FailedCount)> CreateItemsBatchAsync(
        List<string> filePaths,
        IProgress<FileProcessingProgress>? progress,
        int baseOffset)
    {
        int count = filePaths.Count;
        if (count == 0) return (new List<FileItem>(), 0);

        var items = new FileItem?[count];
        var progressTracker = new BatchProgressTracker(progress, baseOffset, count);

        // 그룹은 순차 처리, 그룹 내부는 병렬 처리
        foreach (var group in GroupPathsByDrive(filePaths))
        {
            int parallelism = await _driveInfoService.GetOptimalParallelismAsync(group.RepresentativePath);
            var options = new ParallelOptions { MaxDegreeOfParallelism = parallelism };

            await Parallel.ForEachAsync(
                group.Indices,
                options,
                async (i, ct) =>
                {
                    items[i] = await _fileScannerService.CreateFileItemAsync(filePaths[i]);
                    progressTracker.ReportProcessed();
                });
        }

        var successfulItems = items.OfType<FileItem>().ToList();
        return (successfulItems, count - successfulItems.Count);
    }

    /// <summary>
    /// 입력에서 처음 등장한 순서를 유지한 채 파일 경로를 드라이브별로 묶습니다.
    /// </summary>
    private static List<DrivePathGroup> GroupPathsByDrive(IReadOnlyList<string> filePaths)
    {
        // 드라이브 루트의 "최초 등장 순서"대로 그룹을 구성한다.
        // 예: D:, C:, D:, E: 입력이면 처리 순서는 D -> C -> E
        var groups = new List<DrivePathGroup>();
        var groupMap = new Dictionary<string, DrivePathGroup>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < filePaths.Count; i++)
        {
            string path = filePaths[i];
            string root = Path.GetPathRoot(path) ?? string.Empty;
            if (!groupMap.TryGetValue(root, out var group))
            {
                group = new DrivePathGroup(path);
                groupMap[root] = group;
                groups.Add(group);
            }

            group.Indices.Add(i);
        }

        return groups;
    }

    /// <summary>
    /// 드라이브별 병렬 처리에 사용할 인덱스 묶음을 보관합니다.
    /// </summary>
    private sealed class DrivePathGroup
    {
        // 같은 드라이브 그룹의 병렬도 계산에 사용할 대표 경로다.
        public string RepresentativePath { get; }
        public List<int> Indices { get; } = new();

        /// <summary>
        /// 해당 드라이브에서 처음 본 경로를 대표 경로로 삼아 드라이브 그룹을 생성합니다.
        /// </summary>
        public DrivePathGroup(string representativePath)
        {
            RepresentativePath = representativePath;
        }
    }

}
