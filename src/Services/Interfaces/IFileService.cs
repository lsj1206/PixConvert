using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using PixConvert.Models;

namespace PixConvert.Services;

/// <summary>
/// 파일 시스템의 실제 조작(읽기 등)을 담당하는 서비스의 인터페이스입니다.
/// </summary>
public interface IFileService
{
    /// <summary>
    /// FileInfo 객체를 기반으로 목록에 표시할 FileItem 객체를 생성합니다.
    /// 이미 조회된 정보를 활용하여 추가적인 디스크 I/O를 방지합니다.
    /// </summary>
    /// <param name="fileInfo">파일의 상세 정보가 포함된 FileInfo 객체</param>
    /// <returns>생성된 FileItem 객체</returns>
    FileItem? CreateFileItem(FileInfo fileInfo);

    /// <summary>
    /// [Single Touch 최적화] 파일 경로를 직접 받아 단일 스트림 세션에서 메타데이터와 시그니처를 동시에 획득합니다.
    /// Exists 체크 및 FileInfo 생성을 생략하여 디스크 접근 횟수를 최소화합니다.
    /// </summary>
    /// <param name="path">파일 경로</param>
    /// <returns>생성된 FileItem 객체 (실패 시 null)</returns>
    Task<FileItem?> CreateFileItemAsync(string path);

    /// <summary>
    /// 파일의 헤더(매직 넘버)를 분석하여 실제 포맷(확장자 형태)을 반환합니다.
    /// </summary>
    /// <param name="path">분석할 파일 경로</param>
    /// <returns>탐지된 확장자 문자열 (예: "png")</returns>
    Task<string> AnalyzeSignatureAsync(string path);

    /// <summary>
    /// 지정된 폴더 경로 내의 모든 파일 정보를 재귀적으로 검색하여 가져옵니다.
    /// FileInfo 객체에 메타데이터가 포함되어 있어 후속 작업에서 디스크 재조회를 생략할 수 있습니다.
    /// </summary>
    /// <param name="folderPath">기점 폴더 경로</param>
    /// <returns>검색된 모든 파일의 FileInfo 객체 목록을 반환합니다.</returns>
    IEnumerable<FileInfo> GetFilesInFolder(string folderPath);
}
