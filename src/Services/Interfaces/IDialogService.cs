namespace PixConvert.Services;

using PixConvert.ViewModels;

/// <summary>
/// 사용자 인터페이스와 상호작용하는 대화 상자 서비스의 인터페이스입니다.
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// 사용자에게 확인(예/아니오) 대화 상자를 표시합니다.
    /// </summary>
    /// <param name="message">표시할 메시지 내용 (이미 번역된 문자열)</param>
    /// <param name="titleKey">대화 상자 제목 리소스 키</param>
    /// <param name="warningMessage">경고 메시지 내용 (이미 번역된 문자열)</param>
    Task<bool> ShowConfirmationAsync(string message, string titleKey, string? warningMessage = null);

    /// <summary>앱 설정 대화 상자를 표시합니다.</summary>
    Task<bool> ShowAppSettingDialogAsync(AppSettingViewModel viewModel);

    /// <summary>변환 설정 대화 상자를 표시합니다.</summary>
    Task<bool> ShowConvertSettingDialogAsync(ConvertSettingViewModel viewModel);
}

