namespace PixConvert.Models;

/// <summary>
/// 애플리케이션의 현재 작업 상태를 나타내는 열거형입니다.
/// </summary>
public enum AppStatus
{
    /// <summary>대기 상태</summary>
    Idle,
    /// <summary>파일 또는 폴더 추가 중</summary>
    FileAdd,
    /// <summary>파일 변환 진행 중</summary>
    Converting,
    /// <summary>일반적인 짧은 처리 중 (삭제, 비우기, 순번 재정렬 등)</summary>
    Processing
}
