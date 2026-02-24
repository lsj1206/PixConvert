using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PixConvert.Models;

namespace PixConvert.Services;

/// <summary>
/// IFileProcessingService의 상세 구현 클래스입니다.
/// </summary>
public class FileProcessingService : IFileProcessingService
{
    private readonly IFileService _fileService;
    private readonly ILogger<FileProcessingService> _logger;
    private readonly ILanguageService _languageService;

    public FileProcessingService(IFileService fileService, ILogger<FileProcessingService> logger, ILanguageService languageService)
    {
        _fileService = fileService;
        _logger = logger;
        _languageService = languageService;
    }

    private string GetString(string key) => _languageService.GetString(key);

    public async Task<FileProcessingResult> ProcessPathsAsync(
        IEnumerable<string> paths,
        int maxItemCount,
        int currentCount,
        IProgress<FileProcessingProgress>? progress = null)
    {
        var result = new FileProcessingResult();
        var rawPaths = paths.ToList();
        result.TotalPathCount = rawPaths.Count;

        // 1. 폴더 경로 분할 및 리스트 초기화
        var folders = rawPaths.Where(Directory.Exists).ToList();
        var otherPaths = rawPaths.Where(p => !Directory.Exists(p)).ToList();
        var newItems = new List<FileItem>();

        // 2. 직접 추가된 파일 처리 (Single Touch + 병렬 시그니처 분석)
        if (otherPaths.Count > 0)
        {
            // 적응형 병렬도 결정 (첫 번째 파일 경로 기준)
            int parallelism = GetOptimalParallelism(otherPaths[0]);
            var options = new ParallelOptions { MaxDegreeOfParallelism = parallelism };

            // ConcurrentBag 대신 배열 슬롯으로 순서 보존
            var items = new FileItem?[otherPaths.Count];
            int processedCount = 0;

            await Parallel.ForEachAsync(
                Enumerable.Range(0, otherPaths.Count),
                options,
                async (i, ct) =>
                {
                    // Single Touch: 스트림 하나로 메타데이터 + 시그니처 동시 획득
                    items[i] = await _fileService.CreateFileItemAsync(otherPaths[i]);

                    // 스레드 안전한 진행률 보고 (100개 단위)
                    int current = Interlocked.Increment(ref processedCount);
                    if (progress != null && (current % 100 == 0 || current == otherPaths.Count))
                    {
                        progress.Report(new FileProcessingProgress
                        {
                            CurrentIndex = current,
                            TotalCount = otherPaths.Count
                        });
                    }
                });

            // null이 아닌 유효한 항목만 수집
            newItems.AddRange(items.Where(item => item != null)!);
        }

        // 3. 폴더 내 파일 처리 (FileInfo 활용 + 병렬 시그니처 분석)
        if (folders.Count > 0)
        {
            var folderFiles = new List<FileInfo>();
            await Task.Run(() =>
            {
                foreach (var folderPath in folders)
                {
                    folderFiles.AddRange(_fileService.GetFilesInFolder(folderPath));
                }
            });

            int folderFileCount = folderFiles.Count;
            if (folderFileCount > 0)
            {
                // FileItem 객체를 먼저 일괄 생성 (메타데이터는 FileInfo에서 즉시 획득)
                var folderItems = new List<FileItem>(folderFileCount);
                foreach (var fileInfo in folderFiles)
                {
                    var item = _fileService.CreateFileItem(fileInfo);
                    if (item != null) folderItems.Add(item);
                }

                // 적응형 병렬도 결정 (폴더 경로 기준)
                int parallelism = GetOptimalParallelism(folders[0]);
                var options = new ParallelOptions { MaxDegreeOfParallelism = parallelism };
                int processedCount = 0;
                int totalExpected = otherPaths.Count + folderItems.Count;

                // 시그니처 분석만 병렬로 수행 (각 FileItem의 FileSignature 필드를 채움)
                await Parallel.ForEachAsync(folderItems, options, async (item, ct) =>
                {
                    item.FileSignature = await _fileService.AnalyzeSignatureAsync(item.Path);

                    // 스레드 안전한 진행률 보고
                    int current = Interlocked.Increment(ref processedCount);
                    if (progress != null && (current % 100 == 0 || current == folderItems.Count))
                    {
                        progress.Report(new FileProcessingProgress
                        {
                            CurrentIndex = newItems.Count + current,
                            TotalCount = totalExpected
                        });
                    }
                });

                newItems.AddRange(folderItems);
            }
        }

        // 4. 정책 검사: 최대 수량 초과 여부
        if (currentCount + newItems.Count > maxItemCount)
        {
            result.IgnoredCount = newItems.Count;
            return result;
        }

        result.NewItems = newItems;
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
