using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
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

    [ObservableProperty] private int _convertProgressPercent;

    public IAsyncRelayCommand OpenConvertSettingCommand { get; }
    public IAsyncRelayCommand ConvertFilesCommand { get; }

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

        OpenConvertSettingCommand = new AsyncRelayCommand(OpenConvertSettingAsync, () => CurrentStatus != AppStatus.Converting);
        ConvertFilesCommand = new AsyncRelayCommand(ConvertFilesAsync, () => CurrentStatus != AppStatus.Converting);
    }

    protected override void OnStatusChanged(AppStatus newStatus)
    {
        OpenConvertSettingCommand.NotifyCanExecuteChanged();
        ConvertFilesCommand.NotifyCanExecuteChanged();
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
            GetString("Btn_ConvertSetting"),
            GetString("Dlg_Confirm"),
            GetString("Dlg_Cancel"));

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
            GetString("Dlg_Title_Convert"));

        if (!isConfirmed) return;

        if (!_presetService.ValidPresetData(out string errorKey))
        {
            _snackbarService.Show(GetString(errorKey), SnackbarType.Error);
            return;
        }

        RequestStatus(AppStatus.Converting);
        using var cts = new CancellationTokenSource();
        // TODO: UI에서 취소 버튼을 활성화하면 cts.Cancel()과 연결 예정 (Phase D)

        try
        {
            int processedCount = 0;
            int totalConvertCount = _fileList.Items.Count(item => item.Status != FileConvertStatus.Unsupported);
            int failCount = 0;
            var settings = GetCurrentSettings();
            string presetName = _presetService.Config.LastSelectedPresetName ?? string.Empty;

            // CPU 사용량에 따른 병렬도 결정 (Defect 2)
            int maxDegree = settings.CpuUsage switch
            {
                CpuUsageOption.Max     => Environment.ProcessorCount,
                CpuUsageOption.Optimal => Math.Max(1, Environment.ProcessorCount * 3 / 4),
                CpuUsageOption.Half    => Math.Max(1, Environment.ProcessorCount / 2),
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

            await Parallel.ForEachAsync(activeFiles, parallelOptions, async (item, token) =>
            {
                // 개별 파일 처리 시작
                item.Status = FileConvertStatus.Processing;
                item.Progress = 0;

                // 진행 상황 메시지 전파 (UI 업데이트용)
                WeakReferenceMessenger.Default.Send(new ConvertProgressMessage
                {
                    FileName = System.IO.Path.GetFileName(item.Path),
                    ProcessedCount = processedCount,
                    TotalCount = totalConvertCount,
                    FailCount = failCount,
                    PresetName = presetName
                });

                try
                {
                    var provider = _engineSelector.GetProvider(item, settings);
                    await provider.ConvertAsync(item, settings, token);
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
                    Interlocked.Increment(ref processedCount);
                    ConvertProgressPercent = (int)((double)processedCount / totalConvertCount * 100.0);

                    // 최종 진행 완료 혹은 실패 후 메시지 갱신
                    WeakReferenceMessenger.Default.Send(new ConvertProgressMessage
                    {
                        FileName = System.IO.Path.GetFileName(item.Path),
                        ProcessedCount = processedCount,
                        TotalCount = totalConvertCount,
                        FailCount = failCount,
                        PresetName = presetName
                    });
                }
            });

            if (failCount > 0)
            {
                _snackbarService.Show(string.Format(GetString("Msg_OperationComplete"), processedCount - failCount), SnackbarType.Warning);
            }
            else
            {
                _snackbarService.Show(string.Format(GetString("Msg_OperationComplete"), processedCount), SnackbarType.Success);
            }
        }
        catch (OperationCanceledException)
        {
            // 루프 외부에서 처리되지 않은 파일들 복구 (Defect 3)
            foreach (var item in _fileList.Items.Where(i => i.Status == FileConvertStatus.Processing))
            {
                item.Status = FileConvertStatus.Pending;
            }
            _snackbarService.Show(GetString("Msg_Convert_Cancel"), SnackbarType.Info);
        }
        finally
        {
            ConvertProgressPercent = 0;
            RequestStatus(AppStatus.Idle);
        }
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
