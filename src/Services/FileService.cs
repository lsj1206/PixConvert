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
public class FileService : IFileService
{
    // 파일 크기 단위 정의
    private static readonly string[] SizeSuffixes = ["B", "KB", "MB", "GB", "TB"];

    private readonly ILogger<FileService> _logger;
    private readonly ILanguageService _languageService;

    public FileService(ILogger<FileService> logger, ILanguageService languageService)
    {
        _logger = logger;
        _languageService = languageService;
    }

    private string GetString(string key) => _languageService.GetString(key);

    public FileItem? CreateFileItem(FileInfo fileInfo)
    {
        // 최적화: 외부에서 이미 검증된 FileInfo를 전달받으므로 추가적인 File.Exists나 new FileInfo 조회를 수행하지 않음
        return new FileItem
        {
            Path = fileInfo.FullName,
            Size = fileInfo.Length,
            DisplaySize = FormatFileSize(fileInfo.Length),
            AddIndex = null
        };
    }

    /// <summary>
    /// 파일의 헤더(매직 넘버)를 분석하여 실제 포맷(확장자 형태)을 반환합니다.
    /// </summary>
    public async Task<string> AnalyzeSignatureAsync(string path)
    {
        if (string.IsNullOrEmpty(path)) return "-";

        try
        {
            // useAsync: true를 설정하여 OS 비동기 I/O 경로를 사용하도록 설정
            await using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 16, useAsync: true))
            {
                byte[] header = new byte[16];
                int bytesRead = await fs.ReadAsync(header.AsMemory(0, 16));
                return GetFormatFromHeader(header, bytesRead);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, GetString("Log_File_SignatureFail"), path);
            return "-";
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
            await using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 16, useAsync: true))
            {
                long length = fs.Length;

                byte[] header = new byte[16];
                int bytesRead = await fs.ReadAsync(header.AsMemory(0, 16));
                string signature = GetFormatFromHeader(header, bytesRead);

                return new FileItem
                {
                    Path = path,
                    Size = length,
                    DisplaySize = FormatFileSize(length),
                    FileSignature = signature,
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
    /// 바이트 헤더를 분석하여 포맷을 반환하는 공통 로직입니다.
    /// </summary>
    private string GetFormatFromHeader(byte[] header, int bytesRead)
    {
        if (bytesRead < 2) return "-";

        // 1. JPEG (FF D8 FF)
        if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return "jpg";

        // 2. PNG 판별 (89 50 4E 47)
        if (bytesRead >= 4 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
            return "png";

        // 3. GIF 판별 (47 49 46 38)
        if (bytesRead >= 4 && header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x38)
            return "gif";

        // 4. BMP 판별 (42 4D)
        if (header[0] == 0x42 && header[1] == 0x4D)
            return "bmp";

        // 5. TIFF 판별 (49 49 2A 00 또는 4D 4D 00 2A)
        if (bytesRead >= 4 &&
            ((header[0] == 0x49 && header[1] == 0x49 && header[2] == 0x2A) ||
             (header[0] == 0x4D && header[1] == 0x4D && header[3] == 0x2A)))
            return "tiff";

        // 6. WEBP 판별 (RIFF .... WEBP)
        if (bytesRead >= 12 &&
            header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
            header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
            return "webp";

        return "-";
    }

    public IEnumerable<FileInfo> GetFilesInFolder(string folderPath)
    {
        var files = new List<FileInfo>();
        var stack = new Stack<string>();
        stack.Push(folderPath);

        while (stack.Count > 0)
        {
            var currentDir = stack.Pop();
            var dirInfo = new DirectoryInfo(currentDir);

            try
            {
                // 현재 폴더의 파일 객체들을 직접 추가 (메타데이터 포함됨)
                foreach (var file in dirInfo.GetFiles())
                {
                    files.Add(file);
                }

                // 하위 폴더들을 스택에 넣어 다음 순회 시 처리
                foreach (var dir in dirInfo.GetDirectories())
                {
                    stack.Push(dir.FullName);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                // 접근 권한이 없는 폴더는 로그만 남기고 무시하여 전체 과정이 터지는 것을 방지합니다.
                _logger.LogWarning(ex, GetString("Log_File_FolderAccessFail"), currentDir);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, GetString("Log_File_FolderTraverseFail"), currentDir);
            }
        }
        return files;
    }

    /// <summary>
    /// 파일 바이트 크기를 사람이 읽기 쉬운 단위(KB, MB 등)로 변환합니다.
    /// </summary>
    /// <param name="bytes">바이트 단위의 파일 크기</param>
    /// <returns>단위가 포함된 문자열 (예: 1.5 MB)</returns>
    private static string FormatFileSize(long bytes)
    {
        if (bytes <= 0) return "0 B";

        // 로그 함수를 이용하여 단위 인덱스 계산
        int i = (int)Math.Log(bytes, 1024);
        i = Math.Min(i, SizeSuffixes.Length - 1);

        double readable = bytes / Math.Pow(1024, i);
        return $"{readable:0.#} {SizeSuffixes[i]}";
    }
}
