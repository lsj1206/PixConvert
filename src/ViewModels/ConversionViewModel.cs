using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.ObjectModel;
using System.Windows.Data;
using PixConvert.Models;
using PixConvert.Services;
using PixConvert.Services.Interfaces;

namespace PixConvert.ViewModels;

/// <summary>
/// 파일 변환 오케스트레이션 및 설정 다이얼로그 호출을 전담하는 뷰모델입니다.
/// </summary>
public partial class ConversionViewModel : ViewModelBase
{
    private readonly IDialogService _dialogService;
    private readonly ISnackbarService _snackbarService;
    private readonly IPresetService _presetService;
    private readonly FileListViewModel _fileList;
    private readonly EngineSelector _engineSelector;
    private readonly Func<ConvertSettingViewModel> _convertSettingVmFactory;
    private CancellationTokenSource? _convertCts;
    private readonly object _processesLock = new();

    /// <summary>현재 활발하게 변환 중인 파일 정보를 담는 모델입니다.</summary>
    public partial class ActiveProcess : ObservableObject
    {
        [ObservableProperty] private string _fileName = string.Empty;
        [ObservableProperty] private string _engineName = string.Empty;
    }

    /// <summary>현재 병렬로 처리 중인 파일 목록 (UI 표시용)</summary>
    public ObservableCollection<ActiveProcess> ActiveProcesses { get; } = new();

    [ObservableProperty] private string _currentCpuUsage = string.Empty;
    [ObservableProperty] private string _currentTargetFormat = string.Empty;

    [ObservableProperty] private int _convertProgressPercent;
    [ObservableProperty] private string _currentFileName = string.Empty;
    [ObservableProperty] private int _processedCount;
    [ObservableProperty] private int _totalConvertCount;
    [ObservableProperty] private int _failCount;

    public bool HasFailures => FailCount > 0;

    public IAsyncRelayCommand OpenConvertSettingCommand { get; }
    public IAsyncRelayCommand ConvertFilesCommand { get; }
    public IAsyncRelayCommand CancelConvertCommand { get; }

    public ConversionViewModel(
        ILogger<ConversionViewModel> logger,
        ILanguageService languageService,
        IDialogService dialogService,
        ISnackbarService snackbarService,
        IPresetService presetService,
        FileListViewModel fileList,
        EngineSelector engineSelector,
        Func<ConvertSettingViewModel> convertSettingVmFactory)
        : base(languageService, logger)
    {
        _dialogService = dialogService;
        _snackbarService = snackbarService;
        _presetService = presetService;
        _fileList = fileList;
        _engineSelector = engineSelector;
        _convertSettingVmFactory = convertSettingVmFactory;

        // UI 스레드 외에서도 컬렉션 업데이트가 가능하도록 동기화 활성화
        BindingOperations.EnableCollectionSynchronization(ActiveProcesses, _processesLock);

        OpenConvertSettingCommand = new AsyncRelayCommand(OpenConvertSettingAsync, () => CurrentStatus != AppStatus.Converting);
        ConvertFilesCommand = new AsyncRelayCommand(ConvertFilesAsync, () => CurrentStatus != AppStatus.Converting);
        CancelConvertCommand = new AsyncRelayCommand(CancelConvertAsync, () => CurrentStatus == AppStatus.Converting);
    }

    protected override void OnStatusChanged(AppStatus newStatus)
    {
        _logger.LogInformation("[ConversionViewModel] OnStatusChanged to {Status}", newStatus);
        OpenConvertSettingCommand.NotifyCanExecuteChanged();
        ConvertFilesCommand.NotifyCanExecuteChanged();
        CancelConvertCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// 변환 설정 다이얼로그를 표시하며, 사용자가 설정을 변경하고 확인을 누르면 설정을 동기화합니다.
    /// </summary>
    private async Task OpenConvertSettingAsync()
    {
        await _presetService.LoadAsync();

        // 팩토리 DI를 통해 뷰모델 인스턴스화
        var vm = _convertSettingVmFactory();
        var view = new PixConvert.Views.Controls.ConvertSettingDialog { DataContext = vm };

        bool isSaved = await _dialogService.ShowCustomDialogAsync(
            view,
            "Btn_ConvertSetting",
            "Dlg_Confirm",
            "Dlg_Cancel");

        if (isSaved)
        {
            vm.SyncToSettings();
            bool saved = await _presetService.SaveAsync();
            if (saved)
                _snackbarService.Show(GetString("Msg_Preset_SaveSuccess"), SnackbarType.Success);
            else
                _snackbarService.Show(GetString("Msg_Preset_SaveError"), SnackbarType.Error);
        }
    }

    /// <summary>
    /// 목록에 있는 파일들에 대해 실제 변환 프로세스를 실행합니다.
    /// </summary>
    private async Task ConvertFilesAsync()
    {
        if (_fileList.Items.Count == 0) return;

        var availableCount = _fileList.Items.Count - _fileList.UnsupportedCount;
        if (availableCount <= 0) return;

        bool isConfirmed = await _dialogService.ShowConfirmationAsync(
            string.Format(GetString("Dlg_Ask_Convert"), availableCount),
            "Dlg_Title_Convert");

        if (!isConfirmed) return;

        if (!_presetService.ValidPresetData(out string errorKey))
        {
            _snackbarService.Show(GetString(errorKey), SnackbarType.Error);
            return;
        }

        RequestStatus(AppStatus.Converting);
        _convertCts = new CancellationTokenSource();
        var cts = _convertCts;

        using var session = new ConversionSession();

        try
        {
            int processedCount = 0;
            int totalConvertCount = _fileList.Items.Count(item => item.Status != FileConvertStatus.Unsupported);
            int failCount = 0;
            var settings = GetCurrentSettings();
            string presetName = _presetService.Config.LastSelectedPresetName ?? string.Empty;

            int procCount = Environment.ProcessorCount;
            int maxDegree = settings.CpuUsage switch
            {
                CpuUsageOption.Max     => procCount <= 12 ? Math.Max(1, procCount - 1) : procCount - 2,
                CpuUsageOption.Optimal => Math.Max(1, procCount * 3 / 4),
                CpuUsageOption.Half    => Math.Max(1, procCount / 2),
                CpuUsageOption.Low     => Math.Max(1, procCount / 4),
                CpuUsageOption.Minimum => 1,
                _                      => 1
            };

            var parallelOptions = new ParallelOptions
            {
                CancellationToken = cts.Token,
                MaxDegreeOfParallelism = maxDegree
            };

            var activeFiles = _fileList.Items
                .Where(item => item.Status != FileConvertStatus.Unsupported)
                .ToList();

            // ── 1. 일괄 변환 시작 로그 ───────────────────────────────────
            _logger.LogInformation(GetString("Log_Conversion_BatchStart"), activeFiles.Count);

            // 요약 정보 설정 (Step 1,3 고도화)
            CurrentCpuUsage = GetString($"Setting_Cpu_{settings.CpuUsage}");

            bool hasStandard = activeFiles.Any(f => !f.IsAnimation);
            bool hasAnimation = activeFiles.Any(f => f.IsAnimation);
            if (hasStandard && hasAnimation)
                CurrentTargetFormat = $"{settings.StandardTargetFormat} / {settings.AnimationTargetFormat}";
            else if (hasAnimation)
                CurrentTargetFormat = settings.AnimationTargetFormat;
            else
                CurrentTargetFormat = settings.StandardTargetFormat;

            long lastUpdateTicks = DateTime.UtcNow.Ticks;

            await Parallel.ForEachAsync(activeFiles, parallelOptions, async (item, token) =>
            {
                // 개별 파일 처리 시작
                item.Status = FileConvertStatus.Processing;
                item.Progress = 0;

                var provider = _engineSelector.GetProvider(item, settings);
                var activeProcess = new ActiveProcess
                {
                    FileName = System.IO.Path.GetFileName(item.Path),
                    EngineName = provider.Name
                };

                lock (_processesLock)
                {
                    ActiveProcesses.Add(activeProcess);
                }

                CurrentFileName = activeProcess.FileName;

                // 진행 상황 임시 알림 (너무 잦은 업데이트 방지)
                long currentTicks = DateTime.UtcNow.Ticks;
                if (new TimeSpan(currentTicks - Interlocked.Read(ref lastUpdateTicks)).TotalMilliseconds > 100)
                {
                    Interlocked.Exchange(ref lastUpdateTicks, currentTicks);
                    WeakReferenceMessenger.Default.Send(new ConvertProgressMessage
                    {
                        FileName = System.IO.Path.GetFileName(item.Path),
                        ProcessedCount = processedCount,
                        TotalCount = totalConvertCount,
                        FailCount = failCount,
                        PresetName = presetName
                    });
                }

                try
                {
                    await provider.ConvertAsync(item, settings, session, token);
                }
                catch (OperationCanceledException)
                {
                    // 취소 시 상태 복구 (Defect 3)
                    item.Status = FileConvertStatus.Pending;
                    throw; // 상위 ForEachAsync로 전파하여 중단 유도
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, GetString("Log_Conversion_FileError"), item.Path);
                    Interlocked.Increment(ref failCount);
                }
                finally
                {
                    int currentProcessed = Interlocked.Increment(ref processedCount);
                    int currentFailCount = Interlocked.CompareExchange(ref failCount, 0, 0); // 현재 값 읽기용
                    int newPercent = (int)((double)currentProcessed / totalConvertCount * 100.0);

                    // UI 비핵심 상태 업데이트 (사이드바 바인딩용)
                    ProcessedCount = currentProcessed;
                    TotalConvertCount = totalConvertCount;
                    FailCount = currentFailCount;
                    OnPropertyChanged(nameof(HasFailures));

                    // 작업 완료된 항목 제거
                    var target = ActiveProcesses.FirstOrDefault(p => p.FileName == System.IO.Path.GetFileName(item.Path));
                    if (target != null)
                    {
                        lock (_processesLock)
                        {
                            ActiveProcesses.Remove(target);
                        }
                    }

                    // UI 스레드 부하 방지: 퍼센트가 변경되었거나 최후의 1개일 때만 업데이트 수행
                    if (newPercent != ConvertProgressPercent || currentProcessed == totalConvertCount)
                    {
                        ConvertProgressPercent = newPercent;

                        // 메신저는 여전히 필요한 곳이 있을 수 있으므로 유지 (단, 내부 업데이트 로직은 뷰모델 속성으로 처리됨)
                        WeakReferenceMessenger.Default.Send(new ConvertProgressMessage
                        {
                            FileName = System.IO.Path.GetFileName(item.Path),
                            ProcessedCount = currentProcessed,
                            TotalCount = totalConvertCount,
                            FailCount = currentFailCount,
                            PresetName = presetName
                        });
                    }
                }
            });

            // 모든 항목이 루프에 진입한 직후 취소된 경우, 활성 작업들이 무사히 완료되어
            // Parallel.ForEachAsync가 예외를 던지지 않고 정상 종료될 수 있습니다.
            // (Option A) 강제로 throw를 발생시키지 않고 명시적 조건 분기로 처리합니다.
            int skippedCount = activeFiles.Count(i => i.Status == FileConvertStatus.Skipped);
            int successCount = activeFiles.Count(i => i.Status == FileConvertStatus.Success);

            if (cts.IsCancellationRequested)
            {
                _snackbarService.Show(GetString("Msg_Convert_Cancelled"), SnackbarType.Info);
            }
            else if (skippedCount > 0 && failCount == 0)
            {
                _snackbarService.Show(
                    string.Format(GetString("Msg_OperationCompleteWithSkipped"),
                        successCount, skippedCount),
                    SnackbarType.Warning);
            }
            else if (failCount > 0)
            {
                _snackbarService.Show(string.Format(GetString("Msg_OperationComplete"), successCount), SnackbarType.Warning);
            }
            else
            {
                _snackbarService.Show(string.Format(GetString("Msg_OperationComplete"), successCount), SnackbarType.Success);
            }
        }
        catch (OperationCanceledException)
        {
            // 루프 외부에서 처리되지 않은 파일들 복구 (Defect 3)
            foreach (var item in _fileList.Items.Where(i => i.Status == FileConvertStatus.Processing))
            {
                item.Status = FileConvertStatus.Pending;
            }
            _snackbarService.Show(GetString("Msg_Convert_Cancelled"), SnackbarType.Info);
        }
        finally
        {
            ConvertProgressPercent = 0;
            ProcessedCount = 0;
            TotalConvertCount = 0;
            FailCount = 0;
            CurrentFileName = string.Empty;
            CurrentCpuUsage = string.Empty;
            CurrentTargetFormat = string.Empty;
            lock (_processesLock)
            {
                ActiveProcesses.Clear();
            }
            OnPropertyChanged(nameof(HasFailures));
            _convertCts?.Dispose();
            _convertCts = null;
            RequestStatus(AppStatus.Idle);
        }
    }

    /// <summary>
    /// 현재 진행 중인 변환 작업을 중단합니다. (사용자 확인 다이얼로그 표시 포함)
    /// </summary>
    private async Task CancelConvertAsync()
    {
        bool confirmed = await _dialogService.ShowConfirmationAsync(
            GetString("Dlg_Ask_CancelConvert"),
            "Dlg_Title_CancelConvert",
            GetString("Dlg_Warn_CancelConvert"));

        if (!confirmed) return;

        _logger.LogInformation("[ConversionViewModel] CancelConvertAsync triggered!");
        _convertCts?.Cancel();
        _snackbarService.ShowProgress(GetString("Msg_Convert_Cancelling"));
    }

    /// <summary>
    /// 현재 선택된 프리셋의 설정 객체를 안전하게 가져옵니다.
    /// </summary>
    private ConvertSettings GetCurrentSettings()
    {
        var preset = _presetService.Config.Presets.FirstOrDefault(p => p.Name == _presetService.Config.LastSelectedPresetName);
        return preset?.Settings ?? new ConvertSettings();
    }
}
