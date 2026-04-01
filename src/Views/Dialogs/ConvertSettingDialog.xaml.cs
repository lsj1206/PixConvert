using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;

namespace PixConvert.Views.Dialogs;

/// <summary>
/// ConvertSettingDialog.xaml에 대한 상호 작용 논리입니다.
/// </summary>
public partial class ConvertSettingDialog : UserControl
{
    public ConvertSettingDialog()
    {
        InitializeComponent();
    }

    private void ScrollViewer_MouseDown(object sender, MouseButtonEventArgs e)
    {
        // 클릭된 요소가 ScrollViewer 본체이거나 내부 배경(StackPanel)인 경우에만 포커스 해제
        // 텍스트박스, 버튼, 콤보박스 클릭 시에는 이벤트를 가로채지 않음
        if (e.OriginalSource is ScrollViewer or StackPanel)
        {
            if (sender is IInputElement element)
            {
                element.Focus();
            }
        }
    }
}
