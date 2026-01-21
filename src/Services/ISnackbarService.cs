namespace PixConvert.Services;

/// <summary>
/// 스낵바 알림의 유형을 정의합니다. 유형에 따라 색상과 아이콘이 결정됩니다.
/// </summary>
public enum SnackbarType
{
    /// <summary>기본 정보 알림</summary>
    Info,
    /// <summary>작업 성공 알림</summary>
    Success,
    /// <summary>일부 성공 또는 경고 알림</summary>
    Warning,
    /// <summary>오류 발생 알림</summary>
    Error
}

/// <summary>
/// 화면 하단에 짧은 알림 메시지(스낵바)를 표시하기 위한 서비스 인터페이스입니다.
/// </summary>
public interface ISnackbarService
{
    /// <summary>
    /// 일반 메시지를 스낵바에 표시합니다.
    /// </summary>
    /// <param name="message">표시할 메시지 내용</param>
    /// <param name="type">알림 유형 (기본값: Info)</param>
    /// <param name="durationMs">표시 지속 시간 (밀리초, 기본값: 2000ms)</param>
    void Show(string message, SnackbarType type = SnackbarType.Info, int durationMs = 2000);

    /// <summary>
    /// 진행률 표시용 스낵바를 활성화합니다. 이 스낵바는 수동으로 닫거나 다른 Show 호출 전까지 유지됩니다.
    /// </summary>
    /// <param name="message">진행 상황을 설명하는 초기 메시지</param>
    void ShowProgress(string message);

    /// <summary>
    /// 현재 활성화된 진행률 표시 스낵바의 텍스트를 업데이트합니다.
    /// </summary>
    /// <param name="message">새로운 진행 상황 메시지</param>
    void UpdateProgress(string message);
}
