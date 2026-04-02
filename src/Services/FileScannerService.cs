using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PixConvert.Models;

namespace PixConvert.Services;

/// <summary>
/// 파일 시스템 조작을 담당하는 서비스 클래스입니다.
/// </summary>
public class FileScannerService : IFileScannerService
{
    private readonly ILogger<FileScannerService> _logger;
    private readonly ILanguageService _languageService;

    private string GetString(string key) => _languageService.GetString(key);

    public FileScannerService(ILogger<FileScannerService> logger, ILanguageService languageService)
    {
        _logger = logger;
        _languageService = languageService;
    }

    /// <summary>
    /// 파일의 헤더(매직 넘버)를 분석하여 실제 포맷(확장자 형태)과 애니메이션 여부를 반환합니다.
    /// </summary>
    public async Task<(string Format, bool IsAnimation)> AnalyzeSignatureAsync(string path)
    {
        if (string.IsNullOrEmpty(path)) return ("-", false);

        try
        {
            // 32바이트로 확장하여 WebP의 상세 플래그 및 AVIF 시퀀스 확인 가능하게 함
            await using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 32, useAsync: true))
            {
                byte[] header = new byte[32];
                int bytesRead = await fs.ReadAsync(header.AsMemory(0, 32));
                return GetFormatFromHeader(header, bytesRead);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, GetString("Log_File_SignatureFail"), path);
            return ("-", false);
        }
    }

    /// <summary>
    /// [Single Touch] 단일 스트림을 통해 메타데이터 조회와 시그니처 분석을 한 번에 수행합니다.
    /// </summary>
    public async Task<FileItem?> CreateFileItemAsync(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;

        try
        {
            // 파일을 한 번만 열어서 모든 정보를 획득 (Single Touch)
            // 32바이트 헤더 읽기로 확장하여 애니메이션 여부 판별
            await using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 32, useAsync: true))
            {
                long length = fs.Length;

                byte[] header = new byte[32];
                int bytesRead = await fs.ReadAsync(header.AsMemory(0, 32));
                var (signature, isAnim) = GetFormatFromHeader(header, bytesRead);

                return new FileItem
                {
                    Path = path,
                    Size = length,
                    FileSignature = signature,
                    IsAnimation = isAnim,
                    AddIndex = null
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, GetString("Log_File_MetaFail"), path);
            return null;
        }
    }

    /// <summary>
    /// 바이트 헤더를 분석하여 포맷과 애니메이션 여부를 반환하는 공통 로직입니다.
    /// 지원 포맷: JPEG, PNG, BMP, WEBP, AVIF, GIF
    /// </summary>
    private static (string Format, bool IsAnimation) GetFormatFromHeader(byte[] header, int bytesRead)
    {
        if (bytesRead < 2) return ("-", false);

        // 1. JPEG (FF D8 FF)
        if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return ("JPEG", false);

        // 2. PNG 판별 (89 50 4E 47)
        if (bytesRead >= 4 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
            return ("PNG", false);

        // 3. GIF 판별 (47 49 46 38)
        if (bytesRead >= 4 && header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x38)
        {
            // GIF89a (89a = 0x39 0x61) 만 애니메이션 지원 가능성이 큼
            bool isAnim = bytesRead >= 6 && header[4] == 0x39 && header[5] == 0x61;
            return ("GIF", isAnim);
        }

        // 4. BMP 판별 (42 4D)
        if (header[0] == 0x42 && header[1] == 0x4D)
            return ("BMP", false);

        // 5. WebP 판별 (RIFF....WebP)
        if (bytesRead >= 12 &&
            header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
            header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
        {
            // WebP 서브타입 판별 (VP8X인 경우에만 애니메이션 지원 가능)
            if (bytesRead >= 16 && header[12] == 0x56 && header[13] == 0x50 && header[14] == 0x38 && header[15] == 0x58)
            {
                // VP8X Chunk의 Flags 바이트 (Offset 20)
                // Bit 1 (0x02) 가 Animation 플래그
                bool isAnim = bytesRead >= 21 && (header[20] & 0x02) != 0;
                return ("WEBP", isAnim);
            }
            return ("WEBP", false);
        }

        // 6. AVIF 판별 (ISO Base Media File Format 기반)
        if (bytesRead >= 12 &&
            header[4] == 0x66 && header[5] == 0x74 && header[6] == 0x79 && header[7] == 0x70 &&
            header[8] == 0x61 && header[9] == 0x76 && header[10] == 0x69 &&
            header[11] == 0x66) // 'avif' (정지 이미지) 브랜드만 지원
        {
            return ("AVIF", false);
        }

        return ("-", false);
    }

    public IEnumerable<FileInfo> GetFilesInFolder(string folderPath)
    {
        // 10만 개 이상의 파일이 있어도 List에 담지 않고 하나씩 반환하기 위해 yield return 사용
        var stack = new Stack<string>();
        stack.Push(folderPath);

        while (stack.Count > 0)
        {
            var currentDir = stack.Pop();
            DirectoryInfo dirInfo;

            try
            {
                dirInfo = new DirectoryInfo(currentDir);
                if (!dirInfo.Exists) continue;
            }
            catch { continue; }

            // 1. 현재 폴더의 파일 목록 가져오기 (try-catch 외부에서 yield 하기 위해 분리)
            FileInfo[]? files = null;
            try
            {
                files = dirInfo.GetFiles();
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, GetString("Log_File_FolderAccessFail"), currentDir);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, GetString("Log_File_FolderTraverseFail"), currentDir);
            }

            if (files != null)
            {
                foreach (var file in files)
                {
                    yield return file;
                }
            }

            // 2. 하위 폴더 목록 가져오기
            DirectoryInfo[]? subDirs = null;
            try
            {
                subDirs = dirInfo.GetDirectories();
            }
            catch { /* 하위 폴더 접근 실패 시 해당 경로는 건너뜀 */ }

            if (subDirs != null)
            {
                foreach (var dir in subDirs)
                {
                    stack.Push(dir.FullName);
                }
            }
        }
    }
}
