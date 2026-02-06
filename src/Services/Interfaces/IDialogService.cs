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
    Task<bool> ShowConfirmationAsync(string message, string title);
}

