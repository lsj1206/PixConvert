namespace PixConvert.Services;

/// <summary>
/// 사용자 인터페이스와 상호작용하는 대화 상자 서비스의 인터페이스입니다.
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// 사용자에게 확인(예/아니오) 대화 상자를 표시합니다.
    /// </summary>
    /// <param name="message">표시할 메시지 내용</param>
    /// <param name="title">대화 상자 제목</param>
    Task<bool> ShowConfirmationAsync(string message, string title);

    /// <summary>
    /// 커스텀 UI 요소를 본문으로 하는 다이얼로그를 표시합니다.
    /// </summary>
    /// <param name="content">다이얼로그 본문에 표시할 요소 (예: UserControl)</param>
    /// <param name="title">다이얼로그 제목</param>
    /// <param name="primaryText">주요 작업 버튼 텍스트 (예: 저장)</param>
    /// <param name="closeText">닫기 버튼 텍스트 (예: 취소)</param>
    /// <returns>주요 작업 버튼을 눌러 창이 닫혔는지 여부를 반환합니다.</returns>
    Task<bool> ShowCustomDialogAsync(object content, string title, string? primaryText = null, string? closeText = null);
}

