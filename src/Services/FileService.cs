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
