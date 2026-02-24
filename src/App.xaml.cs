using System;
using System.Windows;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
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
    /// App 인스턴스를 초기화하고 로거 및 서비스 구성을 시작합니다.
    /// </summary>
    public App()
    {
        // 기본 로그 저장 위치: 실행 파일(또는 배포 시) 하위의 logs 폴더
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string logFilePath = System.IO.Path.Combine(baseDir, "logs", "pixconvert_log_.txt");

#if DEBUG
        // 개발 환경(DEBUG)에서는 src와 동일한 레벨(프로젝트 최상위)의 logs 폴더로 지정
        var dir = new System.IO.DirectoryInfo(baseDir);
        while (dir != null && dir.Name != "src")
        {
            dir = dir.Parent;
        }
        if (dir?.Parent != null)
        {
            logFilePath = System.IO.Path.Combine(dir.Parent.FullName, "logs", "pixconvert_log_.txt");
        }
#endif

        // 1. Serilog 전역 로거 구성 (DI 조립 전 발생하는 치명적 에러 캐치용)
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Async(a => a.File(logFilePath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7))
            .CreateLogger();

        // 2. 글로벌 예외 방어벽 구축
        SetupGlobalExceptionHandling();

        // 3. 서비스 구성
        Services = ConfigureServices();
    }

    /// <summary>
    /// 처리되지 않은 모든 전역 예외를 잡아 로깅하고 앱의 강제 종료를 방어합니다.
    /// </summary>
    private void SetupGlobalExceptionHandling()
    {
        // UI 스레드 예외
        this.DispatcherUnhandledException += (s, e) =>
        {
            Log.Fatal(e.Exception, GetLogString("Log_UI_UnhandledException"));
            e.Handled = true; // 강제 종료 방지
        };

        // Task 백그라운드 스레드 예외
        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            Log.Fatal(e.Exception, GetLogString("Log_Background_UnhandledException"));
            e.SetObserved();
        };

        // 기타 AppDomain 예외
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                Log.Fatal(ex, GetLogString("Log_AppDomain_UnhandledException"));
        };
    }

    /// <summary>
    /// 애플리케이션에서 사용할 각종 서비스와 뷰모델을 컨테이너에 등록합니다.
    /// </summary>
    /// <returns>구성된 서비스 제공자 인스턴스</returns>
    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // [Logging] Serilog DI 등록
        services.AddLogging(loggingBuilder =>
        {
            loggingBuilder.AddSerilog(dispose: true);
        });

        // [Services] 싱글톤 서비스 등록
        services.AddSingleton<IWindowService, WindowService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<ISnackbarService, SnackbarService>();
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

            Log.Information(GetLogString("Log_App_Start"));

            // ModernWpf 테마를 Light(밝게) 모드로 설정
            ThemeManager.Current.ApplicationTheme = ApplicationTheme.Light;

            // 메인 윈도우 생성 및 뷰모델 연결 후 표시
            var mainWindow = Services.GetRequiredService<MainWindow>();
            mainWindow.DataContext = Services.GetRequiredService<MainViewModel>();
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, GetLogString("Log_App_FatalInit"));
            MessageBox.Show($"애플리케이션 시작 중 오류가 발생했습니다:\n{ex.Message}",
                            "시작 오류", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information(GetLogString("Log_App_End"));
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    /// <summary>
    /// 안전하게 리소스에서 로그 텍스트를 파싱하여 반환합니다.
    /// 초기화 전이거나 오류 발생 시 키 문자열 자체를 반환합니다.
    /// </summary>
    private static string GetLogString(string key)
    {
        try
        {
            if (Current != null)
            {
                var val = Current.TryFindResource(key);
                if (val != null) return val.ToString() ?? key;
            }
        }
        catch { }
        return key;
    }
}
