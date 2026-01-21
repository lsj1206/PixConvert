using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace PixConvert.Services;

/// <summary>
/// MVVM 패턴을 준수하면서 뷰모델 계층에서 새 창을 열 수 있도록 지원하는 창 관리 서비스 클래스입니다.
/// </summary>
public class WindowService : IWindowService
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// WindowService의 새 인스턴스를 초기화합니다.
    /// </summary>
    /// <param name="serviceProvider">종속성 주입을 위한 서비스 제공자</param>
    public WindowService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// 지정된 타입의 창 인스턴스를 생성하고 모달 형태로 표시합니다.
    /// </summary>
    /// <typeparam name="T">창 클래스 타입</typeparam>
    /// <param name="viewModel">연결할 뷰모델</param>
    /// <returns>대화 상자의 결과(DialogResult)</returns>
    public bool? ShowDialog<T>(object viewModel) where T : Window
    {
        // 컨테이너에서 창 인스턴스를 가져와 속성 설정
        var window = _serviceProvider.GetRequiredService<T>();
        window.DataContext = viewModel;
        window.Owner = Application.Current.MainWindow; // 메인 윈도우를 소유자로 설정

        return window.ShowDialog();
    }

    /// <summary>
    /// 지정된 타입의 창 인스턴스를 생성하고 일반 모드로 표시합니다.
    /// </summary>
    /// <typeparam name="T">창 클래스 타입</typeparam>
    /// <param name="viewModel">연결할 뷰모델</param>
    public void Show<T>(object viewModel) where T : Window
    {
        var window = _serviceProvider.GetRequiredService<T>();
        window.DataContext = viewModel;
        window.Owner = Application.Current.MainWindow;

        window.Show();
    }
}
