using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Mvvm.ComponentModel;
using PixConvert.Models;
using PixConvert.Services;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using System.Diagnostics;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;

namespace PixConvert.ViewModels;

/// <summary>
/// 상단 헤더의 상태 정보(합계, 미지원) 및 언어 설정을 관리하는 뷰모델입니다.
/// </summary>
public partial class HeaderViewModel : ViewModelBase
{
    private readonly FileListViewModel _fileList;
    private readonly IDialogService _dialogService;
    private readonly Func<AppSettingViewModel> _settingsFactory;
    private readonly DispatcherTimer _resourceTimer;

    // CPU 계산용 필드
    private DateTime _lastCpuCheckTime = DateTime.UtcNow;
    private TimeSpan _lastTotalProcessorTime = TimeSpan.Zero;

    /// <summary>
    /// HeaderViewModel의 새 인스턴스를 초기화하며 필요한 서비스를 주입받고 초기 상태를 설정합니다.
    /// </summary>
    public HeaderViewModel(ILanguageService languageService, ILogger<HeaderViewModel> logger,
        FileListViewModel fileList, IDialogService dialogService, Func<AppSettingViewModel> settingsFactory)
        : base(languageService, logger)
    {
        _fileList = fileList;
        _dialogService = dialogService;
        _settingsFactory = settingsFactory;

        // 초기 언어 설정 동기화
        var systemLang = _languageService.GetSystemLanguage();
        SelectedLanguage = Languages.FirstOrDefault(l => l.Code == systemLang) ?? Languages[0];

        // FileListViewModel의 속성 변경 알림을 구독하여 자신의 통계 속성도 함께 알림(UI 동기화)
        _fileList.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(FileListViewModel.TotalCount))
                OnPropertyChanged(nameof(TotalCount));
            else if (e.PropertyName == nameof(FileListViewModel.UnsupportedCount))
                OnPropertyChanged(nameof(UnsupportedCount));
        };

        WeakReferenceMessenger.Default.Register<ConvertProgressMessage>(this, (r, m) =>
        {
            ProcessedCount = m.ProcessedCount;
            TotalConvertCount = m.TotalCount;
            FailCount = m.FailCount;
            OnPropertyChanged(nameof(HasFailures));
        });

        // 리소스 모니터링 타이머 설정 (1초 주기)
        try
        {
            using var proc = Process.GetCurrentProcess();
            _lastTotalProcessorTime = proc.TotalProcessorTime;
            _lastCpuCheckTime = DateTime.UtcNow;
        }
        catch { }

        _resourceTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _resourceTimer.Tick += (s, e) => UpdateResourceUsage();
        _resourceTimer.Start();
    }

    /// <summary>애플리케이션에서 지원하는 언어 목록</summary>
    public ObservableCollection<LanguageOption> Languages { get; } =
    [
        new() { Display = "EN", Code = "en-US" },
        new() { Display = "KR", Code = "ko-KR" }
    ];

    /// <summary>현재 선택된 언어 옵션</summary>
    [ObservableProperty] private LanguageOption selectedLanguage;

    /// <summary>목록의 전체 파일 수 (FileList 위임)</summary>
    public int TotalCount => _fileList.TotalCount;

    /// <summary>미지원(시그니처 미판별) 파일 수 (FileList 위임)</summary>
    public int UnsupportedCount => _fileList.UnsupportedCount;

    // --- 변환 진행 상태 관리 속성 ---
    [ObservableProperty] private string _currentFileName = string.Empty;
    [ObservableProperty] private int _processedCount;
    [ObservableProperty] private int _totalConvertCount;
    [ObservableProperty] private int _failCount;

    public bool HasFailures => FailCount > 0;

    // --- 시스템 리소스 모니터링 ---
    [ObservableProperty] private string _cpuUsageText = "0%";
    [ObservableProperty] private string _memoryUsageText = "0 MB";

    private void UpdateResourceUsage()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            var currentTime = DateTime.UtcNow;

            // 1. CPU 사용량 계산 (프로세스 전용)
            var currentCpuTime = process.TotalProcessorTime;
            var elapsedMs = (currentTime - _lastCpuCheckTime).TotalMilliseconds;

            if (elapsedMs > 500) // 최소 0.5초 간격 유지
            {
                var cpuDeltaMs = (currentCpuTime - _lastTotalProcessorTime).TotalMilliseconds;
                double cpuUsage = (cpuDeltaMs / elapsedMs / Environment.ProcessorCount) * 100;

                // 0~100 사이로 보정
                cpuUsage = Math.Min(100.0, Math.Max(0.0, cpuUsage));
                CpuUsageText = $"{(int)cpuUsage}%";

                _lastCpuCheckTime = currentTime;
                _lastTotalProcessorTime = currentCpuTime;
            }

            // 2. RAM 사용량 (Private Working Set)
            long memBytes = process.PrivateMemorySize64;
            MemoryUsageText = $"{(memBytes / 1024.0 / 1024.0):N1} MB";
        }
        catch { }
    }

    [RelayCommand]
    private async Task ShowAppSettingAsync()
    {
        var vm = _settingsFactory();
        var view = new Views.Controls.AppSettingDialog { DataContext = vm };

        await _dialogService.ShowCustomDialogAsync(
            view,
            (string)App.Current.TryFindResource("Dlg_Title_AppSetting"),
            null,
            (string)App.Current.TryFindResource("Dlg_Confirm"));

        // 언어 표시 동기화
        var currentLang = _languageService.GetCurrentLanguage();
        SelectedLanguage = Languages.FirstOrDefault(l => l.Code == currentLang) ?? Languages[0];
    }
}
