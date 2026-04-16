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
    private readonly IFileScannerService _fileScannerService;
    private readonly ILogger<FileAnalyzerService> _logger;
    private readonly ILanguageService _languageService;
    private readonly IDriveInfoService _driveInfoService;

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

        if (fileCapacity <= 0)
        {
            result.IgnoredCount = rawPaths.Count;
            _logger.LogWarning(GetString("Log_Process_LimitReached"), maxItemCount, rawPaths.Count);
            return result;
        }

        var folders = rawPaths.Where(Directory.Exists).ToList();
        var directFiles = rawPaths.Where(p => !Directory.Exists(p)).ToList();
        var allItems = new List<FileItem>();

        // 1단계: 직접 선택된 파일 처리
        if (directFiles.Count > 0)
        {
            var (items, dups, ignored) = await ProcessDirectFilesAsync(
                directFiles, seenPaths, fileCapacity, progress, allItems.Count);

            allItems.AddRange(items);
            result.DuplicateCount += dups;
            result.IgnoredCount += ignored;
            fileCapacity -= items.Count;
        }

        // 2단계: 폴더 스캔 처리
        if (folders.Count > 0)
        {
            var (items, dups, ignored) = await ProcessFoldersAsync(
                folders, seenPaths, fileCapacity, progress, allItems.Count);

            allItems.AddRange(items);
            result.DuplicateCount += dups;
            result.IgnoredCount += ignored;
        }

        result.NewItems = allItems;
        sw.Stop();

        _logger.LogInformation(GetString("Log_Process_Summary"),
            result.TotalPathCount, result.SuccessCount,
            result.DuplicateCount, result.IgnoredCount, sw.ElapsedMilliseconds);

        return result;
    }

    /// <summary>
    /// 직접 선택된 파일 경로 목록에서 중복을 제거하고 FileItem 배치를 생성합니다.
    /// </summary>
    private async Task<(List<FileItem> Items, int DuplicateCount, int IgnoredCount)>
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
            return (new List<FileItem>(), duplicateCount, ignoredCount);

        var targets = unique.Take(canAdd).ToList();
        var items = await CreateItemsBatchAsync(targets, progress, alreadyProcessedCount);

        return (items, duplicateCount, ignoredCount);
    }

    /// <summary>
    /// 폴더 경로 목록을 재귀 스캔하여 중복·용량 한도 처리 후 FileItem 배치를 생성합니다.
    /// </summary>
    private async Task<(List<FileItem> Items, int DuplicateCount, int IgnoredCount)>
        ProcessFoldersAsync(
            List<string> folders,
            HashSet<string> seenPaths,
            int fileCapacity,
            IProgress<FileProcessingProgress>? progress,
            int alreadyProcessedCount)
    {
        if (fileCapacity <= 0)
            return (new List<FileItem>(), 0, folders.Count);

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
            return (new List<FileItem>(), duplicateCount, ignoredCount);

        var paths = folderFiles.Select(f => f.FullName).ToList();
        var items = await CreateItemsBatchAsync(paths, progress, alreadyProcessedCount);

        return (items, duplicateCount, ignoredCount);
    }

    /// <summary>
    /// 파일 경로 목록을 Parallel.ForEachAsync로 병렬 처리하여 FileItem 컬렉션을 반환합니다.
    /// 병렬도는 IDriveInfoService에 위임합니다. 진행률 보고를 포함합니다.
    /// </summary>
    private async Task<List<FileItem>> CreateItemsBatchAsync(
        List<string> filePaths,
        IProgress<FileProcessingProgress>? progress,
        int baseOffset)
    {
        int count = filePaths.Count;
        if (count == 0) return new List<FileItem>();

        var items = new FileItem?[count];
        int processedCount = 0;

        // 드라이브 루트의 "최초 등장 순서"대로 그룹을 구성한다.
        // 예: D:, C:, D:, E: 입력이면 처리 순서는 D -> C -> E
        var groups = new List<DrivePathGroup>();
        var groupMap = new Dictionary<string, DrivePathGroup>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < count; i++)
        {
            string root = Path.GetPathRoot(filePaths[i]) ?? string.Empty;
            if (!groupMap.TryGetValue(root, out var group))
            {
                group = new DrivePathGroup(root);
                groupMap[root] = group;
                groups.Add(group);
            }
            group.Indices.Add(i);
        }

        // 그룹은 순차 처리, 그룹 내부는 병렬 처리
        foreach (var group in groups)
        {
            int representativeIndex = group.Indices[0];
            int parallelism = await _driveInfoService.GetOptimalParallelismAsync(filePaths[representativeIndex]);
            var options = new ParallelOptions { MaxDegreeOfParallelism = parallelism };

            await Parallel.ForEachAsync(
                group.Indices,
                options,
                async (i, ct) =>
                {
                    items[i] = await _fileScannerService.CreateFileItemAsync(filePaths[i]);
                    int current = Interlocked.Increment(ref processedCount);
                    if (progress != null && (current % 100 == 0 || current == count))
                    {
                        progress.Report(new FileProcessingProgress
                        {
                            CurrentIndex = baseOffset + current,
                            TotalCount = baseOffset + count
                        });
                    }
                });
        }

        return items.Where(item => item != null).ToList()!;
    }

    private sealed class DrivePathGroup
    {
        public string Root { get; }
        public List<int> Indices { get; } = new();

        public DrivePathGroup(string root)
        {
            Root = root;
        }
    }

}
