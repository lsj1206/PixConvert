using System.Collections.Generic;
using PixConvert.Models;

namespace PixConvert.Services;

/// <summary>
/// 정렬 기준 유형 정의
/// </summary>
public enum SortType
{
    /// <summary>목록 추가 순서</summary>
    AddIndex,
    /// <summary>파일 이름 (숫자 인식 정렬)</summary>
    NameIndex,
    /// <summary>파일 이름 및 경로</summary>
    NamePath,
    /// <summary>경로 및 번호</summary>
    PathIndex,
    /// <summary>경로 및 파일 이름</summary>
    PathName,
    /// <summary>파일 크기</summary>
    Size,
    /// <summary>파일 생성 날짜</summary>
    CreatedDate,
    /// <summary>파일 수정 날짜</summary>
    ModifiedDate
}

/// <summary>
/// 정렬 옵션 정보를 담는 클래스
/// </summary>
public class SortOption
{
    public string Display { get; set; } = string.Empty;
    public SortType Type { get; set; }
}

/// <summary>
/// 파일 목록을 정렬하는 기능을 제공하는 서비스의 인터페이스입니다.
/// </summary>
public interface ISortingService
{
    /// <summary>
    /// 지정된 정렬 조건과 방향에 따라 파일 목록을 정렬하여 반환합니다.
    /// </summary>
    /// <param name="items">정렬할 원본 파일 목록</param>
    /// <param name="option">정렬 기준 옵션 (이름, 크기 등)</param>
    /// <param name="isAscending">오름차순 여부 (true: 오름차순, false: 내림차순)</param>
    /// <returns>정렬된 파일 목록의 열거형</returns>
    IEnumerable<FileItem> Sort(IEnumerable<FileItem> items, SortOption option, bool isAscending);
}
