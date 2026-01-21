using CommunityToolkit.Mvvm.ComponentModel;
using PixConvert.Services;

namespace PixConvert.ViewModels;

/// <summary>
/// 화면 상단에 표시되는 알림(스낵바)의 상태 데이터를 관리하는 뷰모델입니다.
/// </summary>
public partial class SnackbarViewModel : ObservableObject
{
    /// <summary>표시할 메시지 내용</summary>
    [ObservableProperty]
    private string message = string.Empty;

    /// <summary>스낵바의 가시성 상태</summary>
    [ObservableProperty]
    private bool isVisible;

    /// <summary>알림의 유형(Info, Success, Warning, Error)</summary>
    [ObservableProperty]
    private SnackbarType type = SnackbarType.Info;

    /// <summary>
    /// 애니메이션 실행 상태 트리거입니다.
    /// View에서는 이 속성을 통해 페이드 인/아웃 효과를 시작합니다.
    /// </summary>
    [ObservableProperty]
    private bool isAnimating;
}
