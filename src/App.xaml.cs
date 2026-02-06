using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using ModernWpf;
using PixConvert.Services;
using PixConvert.ViewModels;

namespace PixConvert;

/// <summary>
/// 애플리케이션의 시작점과 전역 설정을 담당하는 클래스입니다.
/// </summary>
public partial class App : Application
{
    /// <summary>앱의 현재 버전 정보</summary>
    public const string Version = "v.alpha";

    /// <summary>현재 활성화된 App 인스턴스에 대한 접근을 지원합니다.</summary>
    public new static App Current => (App)Application.Current;

    /// <summary>종속성 주입을 위한 중앙 서비스 컨테이너</summary>
    public IServiceProvider Services { get; }

    /// <summary>
    /// App 인스턴스를 초기화하고 서비스 구성을 시작합니다.
    /// </summary>
    public App()
    {
        Services = ConfigureServices();
    }

    /// <summary>
    /// 애플리케이션에서 사용할 각종 서비스와 뷰모델을 컨테이너에 등록합니다.
    /// </summary>
    /// <returns>구성된 서비스 제공자 인스턴스</returns>
    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // [Services] 싱글톤 서비스 등록
        services.AddSingleton<IWindowService, WindowService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<ISnackbarService, SnackbarService>();
        services.AddSingleton<IIconService, IconService>();
        services.AddSingleton<IFileService, FileService>();
        services.AddSingleton<ISortingService, SortingService>();
        services.AddSingleton<ILanguageService, LanguageService>();
        services.AddSingleton<IFileProcessingService, FileProcessingService>();

        // [ViewModels] 화면 상태 관리 뷰모델 등록
        services.AddTransient<MainViewModel>();
        services.AddSingleton<SnackbarViewModel>();
        services.AddSingleton<SettingsViewModel>();

        // [Views] UI 창 등록
        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 애플리케이션이 시작될 때 호출되며, 테마 설정과 메인 윈도우 표시를 수행합니다.
    /// </summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            base.OnStartup(e);

            // ModernWpf 테마를 Light(밝게) 모드로 설정
            ThemeManager.Current.ApplicationTheme = ApplicationTheme.Light;

            // 메인 윈도우 생성 및 뷰모델 연결 후 표시
            var mainWindow = Services.GetRequiredService<MainWindow>();
            mainWindow.DataContext = Services.GetRequiredService<MainViewModel>();
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            // 실행 중 예기치 못한 오류 발생 시 오류 내용을 알리고 종료
            MessageBox.Show($"애플리케이션 시작 중 오류가 발생했습니다:\n{ex.Message}\n\n상세 정보:\n{ex.StackTrace}",
                            "시작 오류", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }

    }
}
