using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace PixConvert.Services;

/// <summary>
/// IDriveInfoService의 구현체입니다.
/// WMI(Windows Management Instrumentation)를 사용하여 드라이브 유형을 판별하며,
/// 성능 향상을 위해 드라이브 루트별로 결과를 캐싱합니다.
/// </summary>
public class DriveInfoService : IDriveInfoService
{
    private readonly ILogger<DriveInfoService> _logger;
    private readonly ILanguageService _languageService;
    
    // 드라이브 루트(예: C:\)별 병렬도 캐시 (런타임 효율화)
    private readonly ConcurrentDictionary<string, int> _parallelismCache = new(StringComparer.OrdinalIgnoreCase);

    private string GetString(string key) => _languageService.GetString(key);

    public DriveInfoService(ILogger<DriveInfoService> logger, ILanguageService languageService)
    {
        _logger = logger;
        _languageService = languageService;
    }

    /// <inheritdoc/>
    public async Task<int> GetOptimalParallelismAsync(string samplePath)
    {
        try
        {
            string root = Path.GetPathRoot(samplePath) ?? "C:\\";
            
            // 캐시 확인: 동일 드라이브 루트인 경우 WMI 호출 생략
            if (_parallelismCache.TryGetValue(root, out int cachedResult))
                return cachedResult;

            var drive = new DriveInfo(root);
            int parallelism;

            if (drive.DriveType == DriveType.Network)
                parallelism = 2;
            else if (drive.DriveType == DriveType.Removable)
                parallelism = 4;
            else if (drive.DriveType == DriveType.Fixed)
            {
                // WMI 호출을 Task.Run으로 격리 — 동기 블로킹을 스레드풀에서 처리
                bool isSsd = await IsSsdAsync(root);
                parallelism = isSsd ? Environment.ProcessorCount : 4;
            }
            else
                parallelism = Math.Min(Environment.ProcessorCount, 8);

            _logger.LogInformation(GetString("Log_Process_Parallelism"), drive.DriveType, parallelism);
            
            // 결과 캐싱 (이 시점에는 root가 확정된 상태이므로 root를 키로 사용)
            _parallelismCache[root] = parallelism;
            return parallelism;
        }
        catch
        {
            // 드라이브 감지 실패 시 안전한 기본값 반환
            return Math.Min(Environment.ProcessorCount, 8);
        }
    }

    /// <summary>
    /// MSFT_PhysicalDisk WMI를 통해 지정된 드라이브가 SSD인지 비동기로 판별합니다.
    /// 동기 WMI 블로킹을 Task.Run으로 완전히 격리합니다.
    /// </summary>
    private Task<bool> IsSsdAsync(string rootPath)
    {
        // Task.Run: ManagementObjectSearcher.Get()은 동기 COM 호출이므로
        // 스레드풀 스레드에서 실행하여 호출 스레드를 보호합니다.
        return Task.Run(() =>
        {
            int diskNumber = -1;
            try
            {
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
                    return false;
                }

                using var physicalSearcher = new ManagementObjectSearcher(
                    @"root\Microsoft\Windows\Storage",
                    $"SELECT MediaType FROM MSFT_PhysicalDisk WHERE DeviceId='{diskNumber}'");

                foreach (ManagementObject physicalDisk in physicalSearcher.Get())
                {
                    var mediaType = physicalDisk["MediaType"];
                    if (mediaType != null && Convert.ToInt32(mediaType) == 4)
                    {
                        _logger.LogInformation(GetString("Log_Process_WmiSsdCheck"), diskNumber, true);
                        return true;
                    }
                    if (mediaType != null && Convert.ToInt32(mediaType) == 3)
                    {
                        _logger.LogInformation(GetString("Log_Process_WmiSsdCheck"), diskNumber, false);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, GetString("Log_Process_WmiException"), rootPath);
                return false;
            }

            _logger.LogWarning(GetString("Log_Process_WmiUnknown"), diskNumber);
            return false;
        });
    }
}
