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

    public FileAnalyzerService(IFileScannerService fileScannerService, ILogger<FileAnalyzerService> logger, ILanguageService languageService)
    {
        _fileScannerService = fileScannerService;
        _logger = logger;
        _languageService = languageService;
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
                int parallelism = GetOptimalParallelism(targetFiles[0]);
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
                int parallelism = GetOptimalParallelism(folders[0]);
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

    /// <summary>
    /// 파일 경로의 드라이브 유형에 따라 최적의 병렬도를 결정합니다.
    /// DriveType 1차 분류 → WMI 2차 분류(Fixed 디스크) → Fallback 순으로 판별합니다.
    /// </summary>
    private int GetOptimalParallelism(string samplePath)
    {
        try
        {
            string root = Path.GetPathRoot(samplePath) ?? "C:\\";
            var drive = new DriveInfo(root);

            int parallelism;

            // 네트워크 드라이브: 동시 연결 제한을 고려하여 최소 병렬
            if (drive.DriveType == DriveType.Network)
                parallelism = 2;
            // 이동식 저장장치(USB 등): 중간 수준 병렬
            else if (drive.DriveType == DriveType.Removable)
                parallelism = 4;
            // 고정 디스크: WMI로 SSD/HDD 판별 시도
            else if (drive.DriveType == DriveType.Fixed)
            {
                if (IsSsd(root))
                    parallelism = Environment.ProcessorCount; // SSD: 최대 병렬
                else
                    parallelism = 4; // HDD: Seek Storm 방지를 위해 제한
            }
            else
            {
                parallelism = Math.Min(Environment.ProcessorCount, 8);
            }

            _logger.LogInformation(GetString("Log_Process_Parallelism"), drive.DriveType, parallelism);
            return parallelism;
        }
        catch { } // 감지 실패 시 안전한 기본값
        return Math.Min(Environment.ProcessorCount, 8);
    }

    /// <summary>
    /// MSFT_PhysicalDisk를 통해 지정된 드라이브가 SSD인지 판별합니다. (Best Effort)
    /// Win32_DiskDrive의 MediaType은 SSD/HDD를 구분하지 못하므로 Storage WMI를 사용합니다.
    /// MSFT_PhysicalDisk.MediaType: 3=HDD, 4=SSD
    /// </summary>
    private bool IsSsd(string rootPath)
    {
        int diskNumber = -1;
        try
        {
            // 1단계: 드라이브 문자 → 디스크 번호 매핑 (Win32_LogicalDisk → Win32_DiskDrive)
            string driveLetter = rootPath.TrimEnd('\\');
            _logger.LogInformation(GetString("Log_Process_WmiDiskNumber"), driveLetter);

            using (var searcher = new ManagementObjectSearcher(
                $"ASSOCIATORS OF {{Win32_LogicalDisk.DeviceID='{driveLetter}'}} WHERE AssocClass=Win32_LogicalDiskToPartition"))
            {
                foreach (ManagementObject partition in searcher.Get())
                {
                    using var diskSearcher = new ManagementObjectSearcher(
                        $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partition["DeviceID"]}'}} WHERE AssocClass=Win32_DiskDriveToDiskPartition");

                    foreach (ManagementObject disk in diskSearcher.Get())
                    {
                        // DeviceID 형식: "\\.\PHYSICALDRIVE0" → 숫자만 추출
                        string deviceId = disk["DeviceID"]?.ToString() ?? "";
                        string numberStr = new string(deviceId.Where(char.IsDigit).ToArray());
                        if (int.TryParse(numberStr, out int num))
                            diskNumber = num;
                    }
                }
            }

            if (diskNumber < 0)
            {
                _logger.LogWarning(GetString("Log_Process_WmiFail"), driveLetter);
                return false; // 매핑 실패 시 HDD 간주 (Seek Storm 방지)
            }

            // 2단계: 디스크 번호로 MSFT_PhysicalDisk 조회 (Storage WMI namespace)
            using var physicalSearcher = new ManagementObjectSearcher(
                @"root\Microsoft\Windows\Storage",
                $"SELECT MediaType FROM MSFT_PhysicalDisk WHERE DeviceId='{diskNumber}'");

            foreach (ManagementObject physicalDisk in physicalSearcher.Get())
            {
                // MediaType: 3 = HDD, 4 = SSD, 5 = SCM
                var mediaType = physicalDisk["MediaType"];
                if (mediaType != null && Convert.ToInt32(mediaType) == 4)
                {
                    _logger.LogInformation(GetString("Log_Process_WmiSsdCheck"), diskNumber, true);
                    return true; // SSD 확인
                }
                if (mediaType != null && Convert.ToInt32(mediaType) == 3)
                {
                    _logger.LogInformation(GetString("Log_Process_WmiSsdCheck"), diskNumber, false);
                    return false; // HDD 확인
                }
            }
        }
        catch (Exception ex)
        {
            // WMI 접근 실패 시 HDD로 간주하여 Seek Storm 억제
            _logger.LogWarning(ex, GetString("Log_Process_WmiException"), rootPath);
            return false;
        }

        // 판별 불가(결과 없음) 시 HDD로 간주 (안전한 쪽으로)
        _logger.LogWarning(GetString("Log_Process_WmiUnknown"), diskNumber);
        return false;
    }
}
