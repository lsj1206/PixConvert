using System.Collections.Generic;
using PixConvert.Models;

namespace PixConvert.Services;

/// <summary>
/// 파일 시스템의 실제 조작(읽기 등)을 담당하는 서비스의 인터페이스입니다.
/// </summary>
public interface IFileService
{
    /// <summary>
    /// 지정된 파일 경로를 분석하여 목록에 표시할 FileItem 객체를 생성합니다.
    /// </summary>
    /// <param name="path">분석할 파일의 전체 경로</param>
    /// <returns>생성된 FileItem 객체를 반환하며, 유효하지 않은 파일인 경우 null을 반환합니다.</returns>
    FileItem? CreateFileItem(string path);

    /// <summary>
    /// 파일의 헤더(매직 넘버)를 분석하여 실제 포맷(확장자 형태)을 반환합니다.
    /// </summary>
    /// <param name="path">분석할 파일 경로</param>
    /// <returns>탐지된 확장자 문자열 (예: "png")</returns>
    Task<string> AnalyzeSignatureAsync(string path);

    /// <summary>
    /// 지정된 폴더 경로 내의 모든 파일 경로를 재귀적으로 검색하여 가져옵니다.
    /// </summary>
    /// <param name="folderPath">기점 폴더 경로</param>
    /// <returns>검색된 모든 파일의 전체 경로 목록을 반환합니다.</returns>
    IEnumerable<string> GetFilesInFolder(string folderPath);
}
