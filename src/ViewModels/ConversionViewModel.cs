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
    private readonly IDialogService _dialogService;
    private readonly ISnackbarService _snackbarService;
    private readonly IPresetService _presetService;
    private readonly FileListViewModel _fileList;
    private readonly EngineSelector _engineSelector;
    private readonly Func<ConvertSettingViewModel> _convertSettingVmFactory;
    private CancellationTokenSource? _convertCts;
    private readonly object _processesLock = new();
    private readonly Stopwatch _stopwatch = new();
    private readonly DispatcherTimer _timer;

    /// <summary>현재 활발하게 변환 중인 파일 정보를 담는 모델입니다.</summary>
    public partial class ActiveProcess : ObservableObject
    {
        [ObservableProperty] private string _fileName = string.Empty;
        [ObservableProperty] private string _engineName = string.Empty;
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
        /// <summary>Interlocked 연산에 사용되는 마지막 진행 알림 시각 (Ticks)</summary>
        public long LastUpdateTicks;

        public ConversionContext(FileListViewModel fileList, IPresetService presetService)
        {
            ActiveFiles = fileList.Items
                .Where(item => item.Status != FileConvertStatus.Unsupported)
                .ToList();
            TotalCount = ActiveFiles.Count;
            PresetName = presetService.Config.LastSelectedPresetName ?? string.Empty;
            LastUpdateTicks = DateTime.UtcNow.Ticks;
        }
    }

    /// <summary>현재 병렬로 처리 중인 파일 목록 (UI 표시용)</summary>
    public ObservableCollection<ActiveProcess> ActiveProcesses { get; } = new();

    [ObservableProperty] private string _currentCpuUsage = string.Empty;
    [ObservableProperty] private string _currentTargetFormat = string.Empty;
    [ObservableProperty] private string _currentQuality = string.Empty;
    [ObservableProperty] private string _currentBgColor = string.Empty;
    [ObservableProperty] private string _currentKeepExif = string.Empty;
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

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _timer.Tick += (s, e) =>
        {
            var elapsed = _stopwatch.Elapsed;
            ConvertingTime = string.Format("{0:00}:{1:00}:{2:00}",
                (int)elapsed.TotalHours,
                elapsed.Minutes,
                elapsed.Seconds);
        };

        // UI 스레드 외에서도 컬렉션 업데이트가 가능하도록 동기화 활성화
        BindingOperations.EnableCollectionSynchronization(ActiveProcesses, _processesLock);

        OpenConvertSettingCommand = new AsyncRelayCommand(OpenConvertSettingAsync, () => CurrentStatus != AppStatus.Converting);
        ConvertFilesCommand = new AsyncRelayCommand(ConvertFilesAsync, () => CurrentStatus != AppStatus.Converting);
        CancelConvertCommand = new AsyncRelayCommand(CancelConvertAsync, () => CurrentStatus == AppStatus.Converting && !IsConversionCompleted);
        ConfirmCompletionCommand = new RelayCommand(ConfirmCompletion, () => IsConversionCompleted);
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
        await _presetService.LoadAsync();

        var vm = _convertSettingVmFactory();
        var view = new PixConvert.Views.Dialogs.ConvertSettingDialog { DataContext = vm };

        bool isSaved = await _dialogService.ShowCustomDialogAsync(view, "Btn_ConvertSetting", "Dlg_Confirm", "Dlg_Cancel");

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
        if (!await ValidateBeforeConvertAsync())
            return;

        RequestStatus(AppStatus.Converting);
        _convertCts = new CancellationTokenSource();
        var cts = _convertCts;

        IsConversionCompleted = false;
        ConfirmCompletionCommand.NotifyCanExecuteChanged();
        CancelConvertCommand.NotifyCanExecuteChanged();

        using var session = new ConversionSession();

        try
        {
            var settings = GetCurrentSettings();
            var context = new ConversionContext(_fileList, _presetService);
            var parallelOptions = BuildParallelOptions(settings.CpuUsage, cts.Token);

            _logger.LogInformation(GetString("Log_Conversion_BatchStart"), context.ActiveFiles.Count);
            UpdateSidebarJobSummary(settings, context.ActiveFiles);

            _stopwatch.Restart();
            ConvertingTime = "00:00:00";
            _timer.Start();

            await Parallel.ForEachAsync(context.ActiveFiles, parallelOptions,
                (item, token) => ProcessSingleFileAsync(item, settings, session, context, token));

            IsConversionCompleted = true;
            ConfirmCompletionCommand.NotifyCanExecuteChanged();
            CancelConvertCommand.NotifyCanExecuteChanged();

            NotifyCompletionResult(cts.IsCancellationRequested, context);
        }
        catch (OperationCanceledException)
        {
            RestorePendingFiles();
            _snackbarService.Show(GetString("Msg_Convert_Cancelled"), SnackbarType.Info);
            ResetConversionState();
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
    private async Task<bool> ValidateBeforeConvertAsync()
    {
        if (_fileList.Items.Count == 0)
            return false;

        var availableCount = _fileList.Items.Count - _fileList.UnsupportedCount;
        if (availableCount <= 0)
            return false;

        bool isConfirmed = await _dialogService.ShowConfirmationAsync(
            string.Format(GetString("Dlg_Ask_Convert"), availableCount), "Dlg_Title_Convert");
        if (!isConfirmed)
            return false;

        if (!_presetService.ValidPresetData(out string errorKey))
        {
            _snackbarService.Show(GetString(errorKey), SnackbarType.Error);
            return false;
        }

        return true;
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
        item.Status = FileConvertStatus.Processing;
        item.Progress = 0;

        var provider = _engineSelector.GetProvider(item, settings);
        var activeProcess = new ActiveProcess
        {
            FileName = Path.GetFileName(item.Path),
            EngineName = provider.Name
        };

        lock (_processesLock) { ActiveProcesses.Add(activeProcess); }
        CurrentFileName = activeProcess.FileName;

        // 진행 상황 사전 알림 (너무 잦은 업데이트 방지)
        long currentTicks = DateTime.UtcNow.Ticks;
        if (new TimeSpan(currentTicks - Interlocked.Read(ref context.LastUpdateTicks)).TotalMilliseconds > 100)
        {
            Interlocked.Exchange(ref context.LastUpdateTicks, currentTicks);
            SendProgressMessage(item, context.ProcessedCount, context.TotalCount, context.FailCount, context.PresetName);
        }

        try
        {
            await provider.ConvertAsync(item, settings, session, token);
        }
        catch (OperationCanceledException)
        {
            item.Status = FileConvertStatus.Pending;
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, GetString("Log_Conversion_FileError"), item.Path);
            Interlocked.Increment(ref context.FailCount);
        }
        finally
        {
            int currentProcessed = Interlocked.Increment(ref context.ProcessedCount);
            int currentFail = Interlocked.CompareExchange(ref context.FailCount, 0, 0);
            int newPercent = (int)((double)currentProcessed / context.TotalCount * 100.0);

            ProcessedCount = currentProcessed;
            TotalConvertCount = context.TotalCount;
            FailCount = currentFail;
            OnPropertyChanged(nameof(HasFailures));

            // 완료된 항목 ActiveProcesses에서 제거
            var target = ActiveProcesses.FirstOrDefault(p => p.FileName == Path.GetFileName(item.Path));
            if (target != null)
                lock (_processesLock) { ActiveProcesses.Remove(target); }

            // 퍼센트 변경 시 또는 마지막 파일일 때만 UI 업데이트
            if (newPercent != ConvertProgressPercent || currentProcessed == context.TotalCount)
            {
                ConvertProgressPercent = newPercent;
                SendProgressMessage(item, currentProcessed, context.TotalCount, currentFail, context.PresetName);
            }
        }
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
        _timer.Stop();
        _stopwatch.Stop();

        if (isCancelled)
        {
            _snackbarService.Show(GetString("Msg_Convert_Cancelled"), SnackbarType.Info);
            return;
        }

        int successCount = context.ActiveFiles.Count(i => i.Status == FileConvertStatus.Success);
        int skippedCount = context.ActiveFiles.Count(i => i.Status == FileConvertStatus.Skipped);

        if (skippedCount > 0 && context.FailCount == 0)
            _snackbarService.Show(string.Format(GetString("Msg_OperationCompleteWithSkipped"), successCount, skippedCount), SnackbarType.Warning);
        else if (context.FailCount > 0)
            _snackbarService.Show(string.Format(GetString("Msg_OperationComplete"), successCount), SnackbarType.Warning);
        else
            _snackbarService.Show(string.Format(GetString("Msg_OperationComplete"), successCount), SnackbarType.Success);
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
        lock (_processesLock) { ActiveProcesses.Clear(); }
        OnPropertyChanged(nameof(HasFailures));
        _convertCts?.Dispose();
        _convertCts = null;
        RequestStatus(AppStatus.Idle);
    }

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
    /// 현재 선택된 프리셋의 설정 객체를 안전하게 가져옵니다.
    /// </summary>
    private ConvertSettings GetCurrentSettings()
    {
        var preset = _presetService.Config.Presets.FirstOrDefault(p => p.Name == _presetService.Config.LastSelectedPresetName);
        return preset?.Settings ?? new ConvertSettings();
    }

    /// <summary>
    /// 사이드바에 표시되는 상태 및 요약 정보(CPU, 포맷, 경로 등)를 갱신합니다.
    /// </summary>
    private void UpdateSidebarJobSummary(ConvertSettings settings, List<FileItem> activeFiles)
    {
        CurrentCpuUsage = GetString($"Setting_Cpu_{settings.CpuUsage}");

        bool hasStandard = activeFiles.Any(f => !f.IsAnimation);
        bool hasAnimation = activeFiles.Any(f => f.IsAnimation);

        if (hasStandard && hasAnimation)
            CurrentTargetFormat = $"{settings.StandardTargetFormat} / {settings.AnimationTargetFormat}";
        else if (hasAnimation)
            CurrentTargetFormat = settings.AnimationTargetFormat;
        else
            CurrentTargetFormat = settings.StandardTargetFormat;

        CurrentQuality = $"{settings.Quality}";
        CurrentBgColor = settings.BackgroundColor;
        CurrentKeepExif = settings.KeepExif ? "O" : "X";
        CurrentOverwritePolicy = GetString($"Setting_Overwrite_{settings.OverwritePolicy}");
        CurrentSaveMethod = settings.FolderMethod == SaveFolderMethod.CreateFolder
            ? settings.OutputSubFolderName
            : GetString($"Setting_SaveMethod_{settings.FolderMethod}");

        if (settings.SaveLocation == SaveLocationType.SameAsOriginal)
        {
            string sameText = GetString("Setting_SaveLocation_Same");
            CurrentSaveLocation = sameText.StartsWith("...") ? sameText : $"...{sameText}";
            CurrentSaveLocationTooltip = string.Empty;
        }
        else
        {
            string targetPath = settings.CustomOutputPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string folderName = Path.GetFileName(targetPath);
            if (string.IsNullOrEmpty(folderName))
                folderName = settings.CustomOutputPath;

            CurrentSaveLocation = $"...{folderName}";
            CurrentSaveLocationTooltip = settings.CustomOutputPath;
        }
    }
}
