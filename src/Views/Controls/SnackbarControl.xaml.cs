using System;
using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using PixConvert.ViewModels;

namespace PixConvert.Views.Controls;

/// <summary>
/// 알림 메시지(스낵바)의 시각적 애니메이션과 상태를 제어하는 컨트롤의 코드 비하인드 클래스입니다.
/// </summary>
public partial class SnackbarControl : UserControl
{
    /// <summary>
    /// SnackbarControl의 새 인스턴스를 초기화하며 데이터 컨텍스트 변경을 감시합니다.
    /// </summary>
    public SnackbarControl()
    {
        InitializeComponent();

        // DataContext가 설정되거나 변경될 때 ViewModel의 속성 변경 알림을 연결합니다.
        DataContextChanged += (s, e) =>
        {
            if (DataContext is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged -= OnViewModelPropertyChanged; // 중복 방지
                npc.PropertyChanged += OnViewModelPropertyChanged;
            }
        };
    }

    /// <summary>
    /// ViewModel의 속성이 변경될 때 호출되며, 애니메이션 실행 여부를 확인합니다.
    /// </summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "IsAnimating")
        {
            UpdateVisualState();
        }
    }

    /// <summary>
    /// ViewModel의 IsAnimating 값에 따라 WPF VisualStateManager를 통해 시각적 상태(Visible/Hidden)를 전환합니다.
    /// </summary>
    private void UpdateVisualState()
    {
        // UI 스레드에서 안전하게 상태 전환 실행
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (DataContext is SnackbarViewModel vm)
            {
                // VisualStateManager를 사용하여 XAML에 정의된 Storyboard 애니메이션을 실행함
                VisualStateManager.GoToState(this, vm.IsAnimating ? "Visible" : "Hidden", true);
            }
        }));
    }
}
