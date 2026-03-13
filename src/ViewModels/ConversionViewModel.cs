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
        Func<ConvertSettingViewModel> convertSettingVmFactory)
        : base(languageService, logger)
    {
        _dialogService = dialogService;
        _snackbarService = snackbarService;
        _presetService = presetService;
        _fileList = fileList;
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
        }

        bool saved = await _presetService.SaveAsync();
        if (saved)
            _snackbarService.Show(GetString("Msg_Preset_SaveSuccess"), SnackbarType.Success);
        else
            _snackbarService.Show(GetString("Msg_Preset_SaveError"), SnackbarType.Error);
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
        try
        {
            int processedCount = 0;
            int totalConvertCount = _fileList.Items.Count(item => item.Status != FileConvertStatus.Unsupported);
            int failCount = 0;
            string presetName = _presetService.Config.LastSelectedPresetName ?? string.Empty;

            foreach (var item in _fileList.Items)
            {
                if (item.Status == FileConvertStatus.Unsupported) continue;

                item.Status = FileConvertStatus.Processing;
                item.Progress = 0;

                WeakReferenceMessenger.Default.Send(new ConvertProgressMessage
                {
                    FileName = System.IO.Path.GetFileName(item.Path),
                    ProcessedCount = processedCount,
                    TotalCount = totalConvertCount,
                    FailCount = failCount,
                    PresetName = presetName
                });

                // 시뮬레이션 로직
                for (int i = 0; i <= 100; i += 20)
                {
                    item.Progress = i;
                    double totalProg = (processedCount + (i / 100.0)) / totalConvertCount * 100.0;
                    ConvertProgressPercent = (int)totalProg;
                    await Task.Delay(100);
                }

                item.Status = FileConvertStatus.Success;
                processedCount++;
                ConvertProgressPercent = (int)((double)processedCount / totalConvertCount * 100.0);
            }
        }
        finally
        {
            ConvertProgressPercent = 0;
            RequestStatus(AppStatus.Idle);
        }
    }
}
