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

        // 1. 기존 파일 집합 (중복 체크용 HashSet 재사용)
        var existingSet = existingPaths ?? (IReadOnlySet<string>)new HashSet<string>();

        // 2. FileCapacity 초기화 (실제 추가 가능한 물리적 공간)
        int fileCapacity = maxItemCount - currentCount;
        if (fileCapacity <= 0)
        {
            result.IgnoredCount = rawPaths.Count;
            _logger.LogWarning(GetString("Log_Process_LimitReached"), maxItemCount, rawPaths.Count);
            return result;
        }

        // 2. 통합 진입점 유지: 입력 경로를 파일과 폴더로 분류
        var folders = rawPaths.Where(Directory.Exists).ToList();
        var otherPaths = rawPaths.Where(p => !Directory.Exists(p)).ToList();
        var newItems = new List<FileItem>();

        // 3. 1단계: 직접 선택된 파일 우선 처리
        if (otherPaths.Count > 0)
        {
            // 중복 파일 분리 (중복 파일은 한도를 소모하지 않음)
            var duplicates = otherPaths.Where(p => existingSet.Contains(p)).ToList();
            var uniqueToProcess = otherPaths.Where(p => !existingSet.Contains(p)).ToList();

            result.DuplicateCount += duplicates.Count;

            int canAdd = Math.Min(uniqueToProcess.Count, fileCapacity);
            result.IgnoredCount += (uniqueToProcess.Count - canAdd);

            if (canAdd > 0)
            {
                var targetFiles = uniqueToProcess.Take(canAdd).ToList();
                int parallelism = await _driveInfoService.GetOptimalParallelismAsync(targetFiles[0]);
                var options = new ParallelOptions { MaxDegreeOfParallelism = parallelism };
                var items = new FileItem?[canAdd];
                int processedCount = 0;

                await Parallel.ForEachAsync(
                    Enumerable.Range(0, canAdd),
                    options,
                    async (i, ct) =>
                    {
                        items[i] = await _fileScannerService.CreateFileItemAsync(targetFiles[i]);
                        int current = Interlocked.Increment(ref processedCount);
                        if (progress != null && (current % 100 == 0 || current == canAdd))
                        {
                            progress.Report(new FileProcessingProgress { CurrentIndex = current, TotalCount = canAdd });
                        }
                    });

                newItems.AddRange(items.Where(item => item != null)!);
                fileCapacity -= newItems.Count;
            }
        }

        // 4. 2단계: 폴더 지연 스캔 (중복 제외 후 남은 공간만큼 채움)
        if (folders.Count > 0 && fileCapacity > 0)
        {
            var folderFiles = new List<FileInfo>();
            await Task.Run(() =>
            {
                foreach (var folderPath in folders)
                {
                    if (fileCapacity <= 0) break;

                    // yield return 반복자를 통한 지연 로딩 스캔 (메모리 효율화)
                    var filesInDir = _fileScannerService.GetFilesInFolder(folderPath);
                    foreach (var file in filesInDir)
                    {
                        // 중복 체크: 이미 리스트에 있는 파일은 한도 계산에서 제외
                        if (existingSet.Contains(file.FullName))
                        {
                            result.DuplicateCount++;
                            continue;
                        }

                        if (fileCapacity > 0)
                        {
                            folderFiles.Add(file);
                            fileCapacity--;
                        }
                        else
                        {
                            result.IgnoredCount++;
                        }
                    }

                    if (fileCapacity <= 0)
                    {
                        _logger.LogWarning(GetString("Log_Process_LimitReached"), maxItemCount, "Scanning...");
                    }
                }
            });

            if (folderFiles.Count > 0)
            {
                int parallelism = await _driveInfoService.GetOptimalParallelismAsync(folders[0]);
                var options = new ParallelOptions { MaxDegreeOfParallelism = parallelism };
                var items = new FileItem?[folderFiles.Count];
                int folderProcessedCount = 0;
                int totalExpected = newItems.Count + folderFiles.Count;

                await Parallel.ForEachAsync(
                    Enumerable.Range(0, folderFiles.Count),
                    options,
                    async (i, ct) =>
                    {
                        // 폴더 내 파일도 분석 시점에 한 번만 열기
                        items[i] = await _fileScannerService.CreateFileItemAsync(folderFiles[i].FullName);

                        int current = Interlocked.Increment(ref folderProcessedCount);
                        if (progress != null && (current % 100 == 0 || current == folderFiles.Count))
                        {
                            progress.Report(new FileProcessingProgress
                            {
                                CurrentIndex = newItems.Count + current,
                                TotalCount = totalExpected
                            });
                        }
                    });

                newItems.AddRange(items.Where(item => item != null)!);
            }
        }
        else if (folders.Count > 0 && fileCapacity <= 0)
        {
            // 폴더라고 할지라도 입력된 경로 자체를 무시 카운트에 합산 (최소한의 성의)
            result.IgnoredCount += folders.Count;
        }

        result.NewItems = newItems;
        sw.Stop();

        _logger.LogInformation(GetString("Log_Process_Summary"),
            result.TotalPathCount, result.SuccessCount, result.DuplicateCount, result.IgnoredCount, sw.ElapsedMilliseconds);

        return result;
    }

}
