using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Threading.Tasks;
using ModernWpf.Controls;

namespace PixConvert.Services;

/// <summary>
/// 사용자 인터페이스와 상호작용하는 다이얼로그 서비스입니다.
/// </summary>
public class DialogService : IDialogService
{
    private readonly ILanguageService _languageService;
    private int _isDialogOpen;

    public DialogService(ILanguageService languageService)
    {
        _languageService = languageService;
    }

    /// <summary>
    /// 예/아니오 선택이 필요한 확인 창을 표시합니다.
    /// 경고 메시지가 전달되면 2단(기본+노란색 경고 텍스트)으로 구성하여 렌더링합니다.
    /// </summary>
    public async Task<bool> ShowConfirmationAsync(string message, string titleKey, string? warningMessage = null)
    {
        // 현재 활성화된 메인 윈도우를 찾아 다이얼로그의 부모로 설정합니다.
        var window = Application.Current.MainWindow;
        if (window == null) return false;
        if (!TryReserveDialog()) return false;

        try
        {
            object dialogContent = message;

            if (!string.IsNullOrWhiteSpace(warningMessage))
            {
                var stackPanel = new System.Windows.Controls.StackPanel { Margin = new System.Windows.Thickness(0, 4, 0, 0) };

                stackPanel.Children.Add(new System.Windows.Controls.TextBlock
                {
                    Text = message,
                    FontSize = 14,
                    TextWrapping = System.Windows.TextWrapping.Wrap
                });

                stackPanel.Children.Add(new System.Windows.Controls.TextBlock
                {
                    Text = warningMessage,
                    FontSize = 12,
                    Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#D97706")), // Dark Orange/Yellow
                    FontFamily = new System.Windows.Media.FontFamily("Segoe UI Emoji, Segoe UI"), // For emoji
                    Margin = new System.Windows.Thickness(0, 12, 0, 0),
                    TextWrapping = System.Windows.TextWrapping.Wrap
                });

                dialogContent = stackPanel;
            }

            var dialog = new ContentDialog
            {
                Content = dialogContent,
                DefaultButton = ContentDialogButton.Close, // Close 버튼을 파란색(Primary)으로 만듦
                Owner = window
            };

            // 좌측(PrimaryButton, 회색) -> 아니오
            // 우측(CloseButton, 파란색) -> 예
            dialog.SetResourceReference(ContentDialog.TitleProperty, string.IsNullOrEmpty(titleKey) ? "Dlg_Confirm" : titleKey);
            dialog.SetResourceReference(ContentDialog.PrimaryButtonTextProperty, "Dlg_No");
            dialog.SetResourceReference(ContentDialog.CloseButtonTextProperty, "Dlg_Yes");

            var result = await dialog.ShowAsync();
            // CloseButton(우측 파란색)을 눌렀을 때가 승인(true)
            return result == ContentDialogResult.None;
        }
        catch (InvalidOperationException ex) when (IsContentDialogAlreadyOpenException(ex))
        {
            return false;
        }
        finally
        {
            ReleaseDialog();
        }
    }

    /// <summary>
    /// 커스텀 UI 요소를 본문으로 하는 다이얼로그를 표시합니다.
    /// </summary>
    public async Task<bool> ShowCustomDialogAsync(object content, string titleKey, string? primaryKey = null, string? closeKey = null)
    {
        var window = Application.Current.MainWindow;
        if (window == null) return false;
        if (!TryReserveDialog()) return false;

        try
        {
            var dialog = new ContentDialog
            {
                Content = content,
                DefaultButton = ContentDialogButton.Close, // Close 버튼을 파란색(Primary)으로 만듦
                Owner = window
            };

            // 실시간 다국어 반영을 위해 리소스 레퍼런스 설정
            dialog.SetResourceReference(ContentDialog.TitleProperty, titleKey);

            // 좌측(PrimaryButton, 취소)
            dialog.SetResourceReference(ContentDialog.PrimaryButtonTextProperty, closeKey ?? "Dlg_Cancel");

            // 우측(CloseButton, 확인)
            if (!string.IsNullOrEmpty(primaryKey))
                dialog.SetResourceReference(ContentDialog.CloseButtonTextProperty, primaryKey);
            else
                dialog.SetResourceReference(ContentDialog.CloseButtonTextProperty, "Dlg_Confirm");

            var contentWidth = GetPreferredContentWidth(content);
            if (contentWidth > 0)
            {
                ApplyFixedDialogWidth(dialog, contentWidth + 50); // 600 + 25*2 기준
            }

            var result = await dialog.ShowAsync();
            // CloseButton(우측 파란색)을 눌렀을 때가 승인(true)
            return result == ContentDialogResult.None;
        }
        catch (InvalidOperationException ex) when (IsContentDialogAlreadyOpenException(ex))
        {
            return false;
        }
        finally
        {
            ReleaseDialog();
        }
    }

    private bool TryReserveDialog()
    {
        return Interlocked.CompareExchange(ref _isDialogOpen, 1, 0) == 0;
    }

    private void ReleaseDialog()
    {
        Interlocked.Exchange(ref _isDialogOpen, 0);
    }

    private static bool IsContentDialogAlreadyOpenException(InvalidOperationException ex)
    {
        return ex.Message.Contains("Only a single ContentDialog", StringComparison.Ordinal);
    }

    private static double GetPreferredContentWidth(object content)
    {
        if (content is not FrameworkElement fe) return 0;
        if (!double.IsNaN(fe.Width) && fe.Width > 0) return fe.Width;
        return fe.MinWidth > 0 ? fe.MinWidth : 0;
    }

    private static void ApplyFixedDialogWidth(ContentDialog dialog, double width)
    {
        // ContentDialog 템플릿의 기본 폭 제한을 우회하기 위해 폭을 고정한다.
        dialog.Resources["ContentDialogMaxWidth"] = width;
        dialog.Resources["ContentDialogMinWidth"] = width;
        dialog.Width = width;
        dialog.MaxWidth = width;
    }
}
