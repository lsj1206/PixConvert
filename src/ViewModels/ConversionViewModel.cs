using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Threading;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.ComponentModel;
using PixConvert.Models;
using PixConvert.Services;
using PixConvert.Services.Interfaces;

namespace PixConvert.ViewModels;

/// <summary>
/// 파일 변환 오케스트레이션 및 설정 다이얼로그 호출을 전담하는 뷰모델입니다.
/// </summary>
public partial class ConversionViewModel : ViewModelBase
{
    private readonly record struct ConversionPreparation(ConvertSettings Settings, string PresetName);

    private readonly record struct ConversionCompletionResult(int SuccessCount, int SkippedCount, int FailedCount);

    private readonly IDialogService _dialogService;
    private readonly ISnackbarService _snackbarService;
    private readonly IPresetService _presetService;
    private readonly FileListViewModel _fileList;
    private readonly IEngineSelector _engineSelector;
    private readonly Func<ConvertSettingViewModel> _convertSettingVmFactory;
    private CancellationTokenSource? _convertCts;
    private readonly Stopwatch _stopwatch = new();
    private readonly DispatcherTimer _timer;
    private readonly Dispatcher _uiDispatcher;

    private static bool CanConvertFile(FileItem item, ConvertSettings settings)
    {
        if (item.IsUnsupported)
            return false;

        if (item.IsAnimation && string.IsNullOrWhiteSpace(settings.AnimationTargetFormat))
            return false;

        return true;
    }

    // ── 변환 작업 공유 카운터 및 파일 목록을 캡슐화하는 컨텍스트 ──────────────
    private sealed class ConversionContext
    {
        public List<FileItem> ActiveFiles { get; }
        public int TotalCount { get; }
        public string PresetName { get; }

        /// <summary>Interlocked 연산에 사용되는 처리 완료 수 카운터</summary>
        public int ProcessedCount;
        /// <summary>Interlocked 연산에 사용되는 실패 수 카운터</summary>
        public int FailCount;
        /// <summary>UI에 마지막으로 반영된 처리 완료 수</summary>
        public int LastAppliedProcessed;
        /// <summary>Interlocked 연산에 사용되는 마지막 진행 알림 시각 (Ticks)</summary>
        public long LastUpdateTicks;

        public ConversionContext(FileListViewModel fileList, ConvertSettings settings, string presetName)
        {
            ActiveFiles = fileList.Items
                .Where(item => CanConvertFile(item, settings))
                .ToList();
            TotalCount = ActiveFiles.Count;
            PresetName = presetName;
            LastUpdateTicks = DateTime.UtcNow.Ticks;
        }
    }

    /// <summary>전체 원본 파일 목록에 대한 접근 (리스트 UI 렌더링용)</summary>
    public ReadOnlyObservableCollection<FileItem> Items => _fileList.Items;

    [ObservableProperty] private string _currentCpuUsage = string.Empty;
    [ObservableProperty] private string _currentTargetFormat = string.Empty;
    [ObservableProperty] private string _currentStandardOptions = string.Empty;
    [ObservableProperty] private string _currentAnimationOptions = string.Empty;
    [ObservableProperty] private bool _showCurrentStandardOptions;
    [ObservableProperty] private bool _showCurrentAnimationOptions;
    [ObservableProperty] private string _currentOverwritePolicy = string.Empty;
    [ObservableProperty] private string _currentSaveMethod = string.Empty;
    [ObservableProperty] private string _currentSaveLocation = string.Empty;
    [ObservableProperty] private string _currentSaveLocationTooltip = string.Empty;

    [ObservableProperty] private int _convertProgressPercent;
    [ObservableProperty] private string _currentFileName = string.Empty;
    [ObservableProperty] private int _processedCount;
    [ObservableProperty] private int _totalConvertCount;
    [ObservableProperty] private int _failCount;
    [ObservableProperty] private string _convertingTime = "00:00:00";
    [ObservableProperty] private bool _isConversionCompleted;

    [ObservableProperty] private string _activePresetName = string.Empty;
    [ObservableProperty] private bool _isActivePresetValid;

    public bool HasFailures => FailCount > 0;

    public IAsyncRelayCommand OpenConvertSettingCommand { get; }
    public IAsyncRelayCommand ConvertFilesCommand { get; }
    public IAsyncRelayCommand CancelConvertCommand { get; }
    public IRelayCommand ConfirmCompletionCommand { get; }

    /// <summary>
    /// ConversionViewModel의 새 인스턴스를 초기화하며 필요한 서비스와 서브 뷰모델들을 구성합니다.
    /// </summary>
    public ConversionViewModel(
        ILogger<ConversionViewModel> logger,
        ILanguageService languageService,
        IDialogService dialogService,
        ISnackbarService snackbarService,
        IPresetService presetService,
        FileListViewModel fileList,
        IEngineSelector engineSelector,
        Func<ConvertSettingViewModel> convertSettingVmFactory)
        : base(languageService, logger)
    {
        _dialogService = dialogService;
        _snackbarService = snackbarService;
        _presetService = presetService;
        _fileList = fileList;
        _engineSelector = engineSelector;
        _convertSettingVmFactory = convertSettingVmFactory;
        _uiDispatcher = Dispatcher.CurrentDispatcher;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _timer.Tick += (s, e) =>
        {
            var elapsed = _stopwatch.Elapsed;
            ConvertingTime = string.Format("{0:00}:{1:00}:{2:00}",
                (int)elapsed.TotalHours,
                elapsed.Minutes,
                elapsed.Seconds);
        };

        OpenConvertSettingCommand = new AsyncRelayCommand(OpenConvertSettingAsync, () => CurrentStatus != AppStatus.Converting);
        ConvertFilesCommand = new AsyncRelayCommand(ConvertFilesAsync, () => CurrentStatus != AppStatus.Converting);
        CancelConvertCommand = new AsyncRelayCommand(CancelConvertAsync, () => CurrentStatus == AppStatus.Converting && !IsConversionCompleted);
        ConfirmCompletionCommand = new RelayCommand(ConfirmCompletion, () => IsConversionCompleted);

        // 초기 프리셋 상태 반영
        RefreshActivePresetUI();
    }

    partial void OnFailCountChanged(int value)
    {
        OnPropertyChanged(nameof(HasFailures));
    }

    /// <summary>
    /// 현재 활성 프리셋의 상태를 UI 프로퍼티에 반영합니다.
    /// </summary>
    private void RefreshActivePresetUI()
    {
        var active = _presetService.ActivePreset;
        if (active != null)
        {
            ActivePresetName = active.Name;
            IsActivePresetValid = true;
        }
        else
        {
            ActivePresetName = GetString("Converting_SelectPreset");
            IsActivePresetValid = false;
        }
    }

    /// <summary>
    /// 상태 변경 시 호출되는 메서드
    /// </summary>
    protected override void OnStatusChanged(AppStatus newStatus)
    {
        _logger.LogInformation("[ConversionViewModel] OnStatusChanged to {Status}", newStatus);
        OpenConvertSettingCommand.NotifyCanExecuteChanged();
        ConvertFilesCommand.NotifyCanExecuteChanged();
        CancelConvertCommand.NotifyCanExecuteChanged();
        ConfirmCompletionCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// 변환 설정 다이얼로그를 표시하며, 사용자가 설정을 변경하고 확인을 누르면 설정을 동기화합니다.
    /// </summary>
    private async Task OpenConvertSettingAsync()
    {
        var vm = _convertSettingVmFactory();

        bool isSaved = await _dialogService.ShowConvertSettingDialogAsync(vm);

        if (isSaved)
        {
            vm.SyncToSettings();
            bool saved = await _presetService.SaveAsync();
            if (saved)
            {
                _snackbarService.Show(GetString("Msg_Preset_SaveSuccess"), SnackbarType.Success);
                // 다이얼로그에서 마지막으로 선택된 프리셋을 활성 프리셋으로 갱신
                if (vm.SelectedPreset != null)
                {
                    _presetService.UpdateActivePreset(vm.SelectedPreset);
                    RefreshActivePresetUI();
                }
            }
            else
            {
                _snackbarService.Show(GetString("Msg_Preset_SaveError"), SnackbarType.Error);
            }
        }
    }

    /// <summary>
    /// 목록에 있는 파일들에 대해 실제 변환 프로세스를 실행합니다.
    /// </summary>
    private async Task ConvertFilesAsync()
    {
        var preparation = await ValidateBeforeConvertAsync();
        if (preparation == null)
            return;

        RequestStatus(AppStatus.Converting);
        _convertCts = new CancellationTokenSource();
        var cts = _convertCts;

        PrepareConversionState();

        using var session = new ConversionSession();

        try
        {
            var context = CreateConversionContext(preparation.Value);
            var parallelOptions = BuildParallelOptions(preparation.Value.Settings.CpuUsage, cts.Token);

            _logger.LogInformation(GetString("Log_Conversion_BatchStart"), context.ActiveFiles.Count);
            UpdateSidebarJobSummary(preparation.Value.Settings, context.ActiveFiles);

            StartConversionClock();

            await RunConversionBatchAsync(context, preparation.Value.Settings, session, parallelOptions);
            CompleteConversion(cts.IsCancellationRequested, context);
        }
        catch (OperationCanceledException)
        {
            await HandleCancelledConversionAsync();
        }
        finally
        {
            if (!IsConversionCompleted)
            {
                ResetConversionState();
            }
        }
    }

    /// <summary>
    /// 변환 시작 전 조건을 검사합니다. (파일 수, 사용자 확인, 프리셋 유효성)
    /// </summary>
    private async Task<ConversionPreparation?> ValidateBeforeConvertAsync()
    {
        if (!HasInputFiles())
        {
            ShowNoTargetFilesMessage();
            return null;
        }

        if (!TryGetActivePreset(out var preset))
        {
            ShowEmptyPresetMessage();
            return null;
        }

        var activePreset = preset!;
        var settings = activePreset.Settings;
        int availableCount = CountConvertibleFiles(settings);
        if (availableCount <= 0)
        {
            ShowNoTargetFilesMessage();
            return null;
        }

        if (!await ConfirmConvertAsync(availableCount))
            return null;

        // 프리셋 데이터 유효성 검사 (자동 보정 없이 에러만 표시)
        if (!ValidatePresetData(settings))
            return null;

        return new ConversionPreparation(settings, activePreset.Name);
    }

    private bool HasInputFiles() => _fileList.Items.Count > 0;

    private int CountConvertibleFiles(ConvertSettings settings) =>
        _fileList.Items.Count(item => CanConvertFile(item, settings));

    private bool TryGetActivePreset(out ConvertPreset? preset)
    {
        preset = _presetService.ActivePreset;
        return preset != null;
    }

    private async Task<bool> ConfirmConvertAsync(int availableCount)
    {
        return await _dialogService.ShowConfirmationAsync(
            string.Format(GetString("Dlg_Ask_Convert"), availableCount),
            "Dlg_Title_Convert");
    }

    private bool ValidatePresetData(ConvertSettings settings)
    {
        if (_presetService.ValidPresetData(settings, out string errorKey))
            return true;

        _snackbarService.Show(GetString(errorKey), SnackbarType.Error);
        return false;
    }

    private void ShowNoTargetFilesMessage()
    {
        _snackbarService.Show(GetString("Msg_Error_NoTargetFiles"), SnackbarType.Warning);
    }

    private void ShowEmptyPresetMessage()
    {
        _snackbarService.Show(GetString("Msg_Error_EmptyPreset"), SnackbarType.Error);
    }

    private void PrepareConversionState()
    {
        IsConversionCompleted = false;
        ConfirmCompletionCommand.NotifyCanExecuteChanged();
        CancelConvertCommand.NotifyCanExecuteChanged();
    }

    private ConversionContext CreateConversionContext(ConversionPreparation preparation)
    {
        return new ConversionContext(_fileList, preparation.Settings, preparation.PresetName);
    }

    private void StartConversionClock()
    {
        _stopwatch.Restart();
        ConvertingTime = "00:00:00";
        _timer.Start();
    }

    private Task RunConversionBatchAsync(
        ConversionContext context,
        ConvertSettings settings,
        ConversionSession session,
        ParallelOptions parallelOptions)
    {
        return Parallel.ForEachAsync(
            context.ActiveFiles,
            parallelOptions,
            (item, token) => ProcessSingleFileAsync(item, settings, session, context, token));
    }

    private void CompleteConversion(bool isCancelled, ConversionContext context)
    {
        IsConversionCompleted = true;
        ConfirmCompletionCommand.NotifyCanExecuteChanged();
        CancelConvertCommand.NotifyCanExecuteChanged();
        NotifyCompletionResult(isCancelled, context);
    }

    private async Task HandleCancelledConversionAsync()
    {
        await RunOnUiAsync(RestorePendingFiles);
        _snackbarService.Show(GetString("Msg_Convert_Cancelled"), SnackbarType.Info);
    }

    /// <summary>
    /// CPU 사용량 옵션에 따라 병렬 실행 옵션을 구성합니다.
    /// </summary>
    private static ParallelOptions BuildParallelOptions(CpuUsageOption cpuUsage, CancellationToken token)
    {
        int procCount = Environment.ProcessorCount;
        int maxDegree = cpuUsage switch
        {
            CpuUsageOption.Max     => procCount <= 12 ? Math.Max(1, procCount - 1) : procCount - 2,
            CpuUsageOption.Optimal => Math.Max(1, procCount * 3 / 4),
            CpuUsageOption.Half    => Math.Max(1, procCount / 2),
            CpuUsageOption.Low     => Math.Max(1, procCount / 4),
            CpuUsageOption.Minimum => 1,
            _                      => 1
        };

        return new ParallelOptions
        {
            CancellationToken = token,
            MaxDegreeOfParallelism = maxDegree
        };
    }

    /// <summary>
    /// 개별 파일 1건에 대한 변환 실행, 진행 상황 업데이트, ActiveProcesses 관리를 수행합니다.
    /// </summary>
    private async ValueTask ProcessSingleFileAsync(
        FileItem item, ConvertSettings settings, ConversionSession session,
        ConversionContext context, CancellationToken token)
    {
        var provider = _engineSelector.GetProvider(item, settings);

        await MarkFileAsProcessingAsync(item, provider.Name, context);

        try
        {
            var result = await provider.ConvertAsync(item, settings, session, token);
            await ApplyConversionResultAsync(item, result);
        }
        catch (OperationCanceledException)
        {
            await RunOnUiAsync(() => item.Status = FileConvertStatus.Pending);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, GetString("Log_Conversion_FileError"), item.Path);
            Interlocked.Increment(ref context.FailCount);
            await RunOnUiAsync(() => item.Status = FileConvertStatus.Error);
        }
        finally
        {
            await RecordProcessedFileAsync(item, context);
        }
    }

    private Task MarkFileAsProcessingAsync(FileItem item, string providerName, ConversionContext context)
    {
        return RunOnUiAsync(() =>
        {
            item.Status = FileConvertStatus.Processing;
            item.Progress = 0;
            item.ProcessingEngine = providerName;
            CurrentFileName = Path.GetFileName(item.Path);

            // 진행 상황 사전 알림 (너무 잦은 업데이트 방지)
            long currentTicks = DateTime.UtcNow.Ticks;
            if (new TimeSpan(currentTicks - Interlocked.Read(ref context.LastUpdateTicks)).TotalMilliseconds > 100)
            {
                Interlocked.Exchange(ref context.LastUpdateTicks, currentTicks);
                SendProgressMessage(item, context.ProcessedCount, context.TotalCount, context.FailCount, context.PresetName);
            }
        });
    }

    private Task RecordProcessedFileAsync(FileItem item, ConversionContext context)
    {
        int currentProcessed = Interlocked.Increment(ref context.ProcessedCount);
        int currentFail = Interlocked.CompareExchange(ref context.FailCount, 0, 0);
        return ApplyProgressUpdateAsync(item, context, currentProcessed, currentFail);
    }

    private Task ApplyConversionResultAsync(FileItem item, ConversionResult result)
    {
        return RunOnUiAsync(() =>
        {
            switch (result.Status)
            {
                case FileConvertStatus.Success:
                    item.OutputSize = result.OutputSize;
                    item.OutputPath = result.OutputPath;
                    item.Progress = 100;
                    item.Status = FileConvertStatus.Success;
                    break;

                case FileConvertStatus.Skipped:
                    item.Status = FileConvertStatus.Skipped;
                    break;

                default:
                    item.Status = result.Status;
                    break;
            }
        });
    }

    private Task ApplyProgressUpdateAsync(
        FileItem item,
        ConversionContext context,
        int currentProcessed,
        int currentFail)
    {
        int newPercent = (int)((double)currentProcessed / context.TotalCount * 100.0);

        return RunOnUiAsync(() =>
        {
            if (currentProcessed < context.LastAppliedProcessed)
                return;

            context.LastAppliedProcessed = currentProcessed;

            ProcessedCount = currentProcessed;
            TotalConvertCount = context.TotalCount;
            FailCount = currentFail;

            // 퍼센트 변경 시 또는 마지막 파일일 때만 UI 업데이트
            if (newPercent != ConvertProgressPercent || currentProcessed == context.TotalCount)
            {
                ConvertProgressPercent = newPercent;
                SendProgressMessage(item, currentProcessed, context.TotalCount, currentFail, context.PresetName);
            }
        });
    }

    private Task RunOnUiAsync(Action action)
    {
        if (_uiDispatcher.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        return _uiDispatcher.InvokeAsync(action).Task;
    }

    /// <summary>
    /// WeakReferenceMessenger로 변환 진행 상황 메시지를 전송합니다.
    /// </summary>
    private static void SendProgressMessage(FileItem item, int processed, int total, int fail, string presetName)
    {
        WeakReferenceMessenger.Default.Send(new ConvertProgressMessage
        {
            FileName = Path.GetFileName(item.Path),
            ProcessedCount = processed,
            TotalCount = total,
            FailCount = fail,
            PresetName = presetName
        });
    }

    /// <summary>
    /// 변환 완료 후 결과에 따라 적절한 스낵바 메시지를 표시합니다.
    /// </summary>
    private void NotifyCompletionResult(bool isCancelled, ConversionContext context)
    {
        StopConversionClock();

        if (isCancelled)
        {
            _snackbarService.Show(GetString("Msg_Convert_Cancelled"), SnackbarType.Info);
            return;
        }

        ShowCompletionSummary(BuildCompletionResult(context));
    }

    private ConversionCompletionResult BuildCompletionResult(ConversionContext context)
    {
        return new ConversionCompletionResult(
            context.ActiveFiles.Count(item => item.Status == FileConvertStatus.Success),
            context.ActiveFiles.Count(item => item.Status == FileConvertStatus.Skipped),
            context.FailCount);
    }

    private void ShowCompletionSummary(ConversionCompletionResult result)
    {
        if (result.SkippedCount > 0 && result.FailedCount > 0)
        {
            _snackbarService.Show(
                string.Format(GetString("Msg_OperationCompleteWithSkippedAndFailures"), result.SuccessCount, result.SkippedCount, result.FailedCount),
                SnackbarType.Warning);
            return;
        }

        if (result.FailedCount > 0)
        {
            _snackbarService.Show(
                string.Format(GetString("Msg_OperationCompleteWithFailures"), result.SuccessCount, result.FailedCount),
                SnackbarType.Warning);
            return;
        }

        if (result.SkippedCount > 0)
        {
            _snackbarService.Show(
                string.Format(GetString("Msg_OperationCompleteWithSkipped"), result.SuccessCount, result.SkippedCount),
                SnackbarType.Warning);
            return;
        }

        _snackbarService.Show(
            string.Format(GetString("Msg_OperationComplete"), result.SuccessCount),
            SnackbarType.Success);
    }

    private void StopConversionClock()
    {
        _timer.Stop();
        _stopwatch.Stop();
    }

    /// <summary>
    /// 취소 시 Processing 상태로 남은 파일을 Pending으로 복구합니다.
    /// </summary>
    private void RestorePendingFiles()
    {
        foreach (var item in _fileList.Items.Where(i => i.Status == FileConvertStatus.Processing))
            item.Status = FileConvertStatus.Pending;
    }

    /// <summary>
    /// 변환 완료/취소 후 모든 UI 상태를 초기화합니다.
    /// </summary>
    private void ResetConversionState()
    {
        StopConversionClock();

        IsConversionCompleted = false;
        ConfirmCompletionCommand.NotifyCanExecuteChanged();
        CancelConvertCommand.NotifyCanExecuteChanged();

        ConvertProgressPercent = 0;
        ProcessedCount = 0;
        TotalConvertCount = 0;
        FailCount = 0;
        CurrentFileName = string.Empty;
        CurrentCpuUsage = string.Empty;
        CurrentTargetFormat = string.Empty;
        CurrentStandardOptions = string.Empty;
        CurrentAnimationOptions = string.Empty;
        ShowCurrentStandardOptions = false;
        ShowCurrentAnimationOptions = false;
        _convertCts?.Dispose();
        _convertCts = null;
        RequestStatus(AppStatus.Idle);
    }

    /// <summary>
    /// 완료 확인 이후 변환 관련 상태를 초기 상태로 되돌립니다.
    /// </summary>
    private void ConfirmCompletion()
    {
        ResetConversionState();
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
    /// 사이드바에 표시되는 상태 및 요약 정보(CPU, 포맷, 경로 등)를 갱신합니다.
    /// </summary>
    private void UpdateSidebarJobSummary(ConvertSettings settings, List<FileItem> activeFiles)
    {
        CurrentCpuUsage = GetString($"Setting_Cpu_{settings.CpuUsage}");
        CurrentTargetFormat = ConversionSummaryBuilder.BuildTargetFormatSummary(settings, activeFiles);
        ApplyOptionSummaries(
            ConversionSummaryBuilder.BuildStandardOptionsSummary(settings, activeFiles, GetString),
            ConversionSummaryBuilder.BuildAnimationOptionsSummary(settings, activeFiles, GetString));
        CurrentOverwritePolicy = GetString($"Setting_Overwrite_{settings.OverwritePolicy}");
        CurrentSaveMethod = ConversionSummaryBuilder.BuildSaveMethodSummary(settings, GetString);
        ApplySaveLocationSummary(ConversionSummaryBuilder.BuildSaveLocationSummary(settings, GetString));
    }

    private void ApplyOptionSummaries(string standardOptions, string animationOptions)
    {
        CurrentStandardOptions = standardOptions;
        CurrentAnimationOptions = animationOptions;
        ShowCurrentStandardOptions = !string.IsNullOrWhiteSpace(CurrentStandardOptions);
        ShowCurrentAnimationOptions = !string.IsNullOrWhiteSpace(CurrentAnimationOptions);
    }

    private void ApplySaveLocationSummary(SaveLocationSummary summary)
    {
        CurrentSaveLocation = summary.DisplayText;
        CurrentSaveLocationTooltip = summary.TooltipText;
    }

}
