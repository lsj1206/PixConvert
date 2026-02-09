using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PixConvert.Models;

namespace PixConvert.Services;

/// <summary>
/// 파일 처리 결과 및 통계를 담는 클래스입니다.
/// </summary>
public class FileProcessingResult
{
    /// <summary>최종적으로 생성된 FileItem 목록</summary>
    public List<FileItem> NewItems { get; set; } = new();

    /// <summary>입력받은 총 경로 수</summary>
    public int TotalPathCount { get; set; }

    /// <summary>이미 존재하여 제외된 파일 수</summary>
    public int DuplicateCount { get; set; }

    /// <summary>최대 개수 제한 등으로 인해 무시된 파일 수</summary>
    public int IgnoredCount { get; set; }

    /// <summary>성공적으로 추가된 실질적인 개수</summary>
    public int SuccessCount => NewItems.Count;
}

/// <summary>
/// 파일 처리 진행 상황을 나타내는 클래스입니다.
/// </summary>
public struct FileProcessingProgress
{
    /// <summary>현재 처리 중인 파일의 인덱스</summary>
    public int CurrentIndex { get; set; }

    /// <summary>전체 처리 대상 파일 수</summary>
    public int TotalCount { get; set; }

    /// <summary>진행률 (0~100)</summary>
    public double Percentage => TotalCount > 0 ? (double)CurrentIndex / TotalCount * 100 : 0;
}

/// <summary>
/// 입력된 경로들을 분석하여 유효한 파일 목록을 추출하고 처리하는 서비스 인터페이스입니다.
/// </summary>
public interface IFileProcessingService
{
    /// <summary>
    /// 파일 및 폴더 경로 목록을 입력받아 재귀적으로 파일을 추출하고 FileItem 목록을 생성합니다.
    /// </summary>
    /// <param name="paths">처리할 파일 또는 폴더 경로 목록</param>
    /// <param name="maxItemCount">허용되는 최대 파일 개수</param>
    /// <param name="currentCount">현재 목록에 존재하는 파일 개수 (중복 및 수량 제한 확인용)</param>
    /// <param name="progress">진행 상황을 보고하기 위한 IProgress 인터페이스</param>
    /// <returns>처리 통계와 새로 생성된 아이템 목록을 담은 FileProcessingResult</returns>
    Task<FileProcessingResult> ProcessPathsAsync(
        IEnumerable<string> paths,
        int maxItemCount,
        int currentCount,
        IProgress<FileProcessingProgress>? progress = null);
}
