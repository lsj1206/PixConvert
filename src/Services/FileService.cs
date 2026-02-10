using System;
using System.IO;
using System.Collections.Generic;
using PixConvert.Models;

namespace PixConvert.Services;

/// <summary>
/// 파일 시스템 조작을 담당하는 서비스 클래스입니다.
/// </summary>
public class FileService : IFileService
{
    private readonly IIconService _iconService;

    // 파일 크기 단위 정의
    private static readonly string[] SizeSuffixes = ["B", "KB", "MB", "GB", "TB"];

    /// <summary>
    /// FileService의 새 인스턴스를 초기화합니다.
    /// </summary>
    public FileService(IIconService iconService)
    {
        _iconService = iconService;
    }

    /// <summary>
    /// 지정된 경로를 분석하여 파일의 상세 정보를 담은 FileItem 객체를 생성합니다.
    /// </summary>
    /// <param name="path">파일 경로</param>
    /// <returns>파일 정보를 담은 FileItem 객체 또는 유효하지 않을 시 null</returns>
    public FileItem? CreateFileItem(string path)
    {
        if (File.Exists(path))
        {
            var fileInfo = new FileInfo(path);
            return new FileItem
            {
                Path = path,
                Size = fileInfo.Length,
                DisplaySize = FormatFileSize(fileInfo.Length),
                CreatedDate = fileInfo.CreationTime,
                ModifiedDate = fileInfo.LastWriteTime,
                AddIndex = null,
                Icon = _iconService.GetIcon(path) // 시스템 아이콘 가져오기
            };
        }
        return null;
    }

    /// <summary>
    /// 파일의 헤더(매직 넘버)를 분석하여 실제 포맷(확장자 형태)을 반환합니다.
    /// </summary>
    public async Task<string> AnalyzeSignatureAsync(string path)
    {
        if (string.IsNullOrEmpty(path)) return "-";

        // 경로 정규화 (시스템 간 호환성 확보)
        string fullPath = Path.GetFullPath(path);

        return await Task.Run(() =>
        {
            try
            {
                if (!File.Exists(fullPath)) return "-";

                using (var fs = File.OpenRead(fullPath))
                {
                    byte[] header = new byte[16];
                    int bytesRead = fs.Read(header, 0, 16);

                    // 최소 시그니처 길이 확인
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
            }
            catch
            {
                // 권한 부족 등 읽기 실패 시 알 수 없음으로 처리
                return "-";
            }
        });
    }

    /// <summary>
    /// 폴더 내의 모든 파일을 하위 폴더까지 포함하여 재귀적으로 검색합니다.
    /// </summary>
    /// <param name="folderPath">검색을 시작할 폴더 경로</param>
    /// <returns>발견된 모든 파일의 전체 경로 목록</returns>
    public IEnumerable<string> GetFilesInFolder(string folderPath)
    {
        var files = new List<string>();
        var stack = new Stack<string>();
        stack.Push(folderPath);

        while (stack.Count > 0)
        {
            var currentDir = stack.Pop();
            var dirInfo = new DirectoryInfo(currentDir);

            try
            {
                // 현재 폴더의 파일들 추가
                foreach (var file in dirInfo.GetFiles())
                {
                    files.Add(file.FullName);
                }

                // 하위 폴더들을 스택에 넣어 다음 순회 시 처리
                foreach (var dir in dirInfo.GetDirectories())
                {
                    stack.Push(dir.FullName);
                }
            }
            catch (UnauthorizedAccessException)
            {
                // 접근 권한이 없는 폴더는 무시하고 진행합니다.
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
