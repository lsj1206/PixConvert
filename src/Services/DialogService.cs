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

    /// <summary>
    /// 커스텀 UI 요소를 본문으로 하는 다이얼로그를 표시합니다.
    /// </summary>
    public async Task<bool> ShowCustomDialogAsync(object content, string title, string? primaryText = null, string? closeText = null)
    {
        var window = Application.Current.MainWindow;
        if (window == null) return false;

        // ContentDialog의 기본 폭 제약으로 인해 내부 UserControl 폭이 잘리는 문제를 방지한다.
        double minWidth = 0;
        if (content is FrameworkElement fe)
        {
            if (!double.IsNaN(fe.Width) && fe.Width > 0)
                minWidth = fe.Width;
            else if (fe.MinWidth > 0)
                minWidth = fe.MinWidth;
        }

        var dialog = new ContentDialog
        {
            Title = title,
            Content = content,
            PrimaryButtonText = primaryText,
            CloseButtonText = closeText ?? _languageService.GetString("Dlg_Close"),
            DefaultButton = ContentDialogButton.Primary,
            Owner = window
        };
        if (minWidth > 0)
        {
            // ContentDialog 템플릿의 기본 폭 제한을 우회하기 위해 실폭을 고정한다.
            double targetWidth = minWidth + 50; // 600 + 25*2 기준
            dialog.Resources["ContentDialogMaxWidth"] = targetWidth;
            dialog.Resources["ContentDialogMinWidth"] = targetWidth;
            dialog.MinWidth = targetWidth;
            dialog.Width = targetWidth;
            dialog.MaxWidth = targetWidth;
        }

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }
}
