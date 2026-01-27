using System;
using System.Windows;
using System.Threading.Tasks;
using ModernWpf.Controls;

namespace PixConvert.Services;

/// <summary>
/// 사용자 인터페이스와 상호작용하는 다이얼로그 서비스입니다.
/// </summary>
public class DialogService : IDialogService
{
    private readonly ILanguageService _languageService;

    public DialogService(ILanguageService languageService)
    {
        _languageService = languageService;
    }

    /// <summary>
    /// 예/아니오 선택이 필요한 확인 창을 표시합니다.
    /// </summary>
    public async Task<bool> ShowConfirmationAsync(string message, string title)
    {
        // 현재 활성화된 메인 윈도우를 찾아 다이얼로그의 부모로 설정합니다.
        var window = Application.Current.MainWindow;
        if (window == null) return false;

        if (string.IsNullOrEmpty(title))
            title = _languageService.GetString("Dlg_Confirm");

        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            PrimaryButtonText = _languageService.GetString("Dlg_Yes"),
            CloseButtonText = _languageService.GetString("Dlg_No"),
            DefaultButton = ContentDialogButton.Primary,
            Owner = window
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }
}
