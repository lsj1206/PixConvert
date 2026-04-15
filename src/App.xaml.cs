using System;
using System.Net.Http;
using System.Windows;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using ModernWpf;
using PixConvert.Services;
using PixConvert.Services.Providers;
using PixConvert.Services.Interfaces;
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

        // 1. Serilog 전역 로거 구성 (DI 조립 전 발생하는 에러 체크)
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Async(a => a.File(logFilePath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7))
            .CreateLogger();

        // 2. 예외 방어벽
        SetupExceptionHandling();

        // 3. 서비스 구성
        Services = ConfigureServices();
    }

    /// <summary>
    /// 처리되지 않은 전역 예외를 잡아 로깅하고 앱의 강제 종료를 방어합니다.
    /// </summary>
    private void SetupExceptionHandling()
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
        // 서비스 컬렉션 생성
        var services = new ServiceCollection();

        // [Logging] Serilog DI 등록
        services.AddLogging(loggingBuilder => loggingBuilder.AddSerilog(dispose: true));

        // [Services] 싱글톤 서비스 등록
        services.AddSingleton<IWindowService, WindowService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<ISnackbarService, SnackbarService>();
        services.AddSingleton<ILanguageService, LanguageService>();
        services.AddSingleton<ISettingService, SettingService>();
        services.AddSingleton<IPresetService, PresetService>();
        services.AddSingleton<IDriveInfoService, DriveInfoService>();
        services.AddSingleton<IFileScannerService, FileScannerService>();
        services.AddSingleton<IFileAnalyzerService, FileAnalyzerService>();
        services.AddSingleton<ISortingService, SortingService>();
        services.AddSingleton(_ =>
        {
            var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("PixConvert");
            return httpClient;
        });
        services.AddSingleton<IExternalLauncher, ExternalLauncher>();
        services.AddSingleton<IAppInfoService, AppInfoService>();

        // [Conversion Engine] 변환 엔진 관련 서비스 등록
        services.AddSingleton<SkiaSharpProvider>();
        services.AddSingleton<NetVipsProvider>();
        services.AddSingleton<EngineSelector>();

        // [ViewModel] 화면 상태 관리 뷰모델 등록
        services.AddTransient<MainViewModel>();
        services.AddSingleton<HeaderViewModel>();
        services.AddSingleton<SnackbarViewModel>();
        services.AddSingleton<FileListViewModel>();
        services.AddSingleton<SortFilterViewModel>();

        // [ViewModel] 사이드바 3분할 뷰모델 등록
        services.AddSingleton<FileInputViewModel>();
        services.AddSingleton<ConversionViewModel>();
        services.AddSingleton<ListManagerViewModel>();

        // 다이얼로그 전용 뷰모델 및 팩토리 패턴 등록
        services.AddTransient<ConvertSettingViewModel>();
        services.AddTransient<Func<ConvertSettingViewModel>>(sp => () => sp.GetRequiredService<ConvertSettingViewModel>());
        services.AddTransient<AppSettingViewModel>();
        services.AddTransient<Func<AppSettingViewModel>>(sp => () => sp.GetRequiredService<AppSettingViewModel>());

        // [View] UI 창 등록
        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 애플리케이션이 시작될 때 호출되며, 테마 설정과 메인 윈도우 표시를 수행합니다.
    /// </summary>
    protected override async void OnStartup(StartupEventArgs e)
    {
        try
        {
            base.OnStartup(e);

            Log.Information(GetLogString("Log_App_Start"));

            // 설정 파일 로드 및 설정 적용
            var settingService = Services.GetRequiredService<ISettingService>();
            await settingService.InitializeAsync();

            // 프리셋 로드
            var presetService = Services.GetRequiredService<IPresetService>();
            await presetService.InitializeAsync();

            // 메인 윈도우 생성 및 뷰모델 연결 후 표시
            var mainWindow = Services.GetRequiredService<MainWindow>();
            mainWindow.DataContext = Services.GetRequiredService<MainViewModel>();
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, GetLogString("Log_App_FatalInit"));
            MessageBox.Show(string.Format(GetLogString("Err_Fatal_Init"), ex.Message),
                            GetLogString("Err_Fatal_Title"), MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    /// <summary>
    /// 애플리케이션이 종료될 때 호출되며, 리소스를 정리합니다.
    /// </summary>
    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information(GetLogString("Log_App_End"));

        try
        {
            // NetVips 네이티브 자원 명시적 해제
            NetVips.NetVips.Shutdown();
        }
        catch { }

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
