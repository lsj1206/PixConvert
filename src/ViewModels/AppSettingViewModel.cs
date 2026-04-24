using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PixConvert.Models;
using PixConvert.Services;
using PixConvert.Services.Interfaces;

namespace PixConvert.ViewModels;

/// <summary>
/// 애플리케이션 전역 설정을 관리하는 뷰모델입니다.
/// </summary>
public partial class AppSettingViewModel : ViewModelBase
{
    private readonly ISettingService _settingService;
    private readonly IAppInfoService _appInfoService;
    private readonly IExternalLauncher _externalLauncher;
    private readonly ListManagerViewModel _listManager;

    [ObservableProperty] private bool _isCheckingUpdate;
    [ObservableProperty] private string _updateStatusText = string.Empty;
    [ObservableProperty] private string _engineInfoText = string.Empty;

    /// <summary>현재 선택된 언어 코드 (en-US, ko-KR 등)</summary>
    [ObservableProperty] private string _currentLanguageCode;

    /// <summary>파일 삭제 시 확인 메시지 표시 여부 (ListManagerViewModel 연동)</summary>
    public bool ConfirmDeletion
    {
        get => _listManager.ConfirmDeletion;
        set
        {
            if (_listManager.ConfirmDeletion != value)
            {
                _settingService.Settings.ConfirmDeletion = value;
                _listManager.ConfirmDeletion = value;
                OnPropertyChanged();
                _ = SaveSettingsAsync();
            }
        }
    }

    /// <summary>
    /// AppSettingViewModel의 새 인스턴스를 초기화합니다.
    /// </summary>
    public AppSettingViewModel(
        ILanguageService languageService,
        ILogger<AppSettingViewModel> logger,
        ISettingService settingService,
        IAppInfoService appInfoService,
        IExternalLauncher externalLauncher,
        ListManagerViewModel listManager)
        : base(languageService, logger)
    {
        _settingService = settingService;
        _appInfoService = appInfoService;
        _externalLauncher = externalLauncher;
        _listManager = listManager;

        // 저장된 설정으로 초기값 동기화
        _listManager.ConfirmDeletion = _settingService.Settings.ConfirmDeletion;

        // 저장된 언어로 초기값 설정
        CurrentLanguageCode = _languageService.GetCurrentLanguage();
        EngineInfoText = string.Join(
            Environment.NewLine,
            _appInfoService.GetEngineInfo().Select(engine => $"{engine.Name} {engine.Version}"));
    }

    /// <summary>
    /// 현재 언어 코드가 변경되었을 때 호출되는 메서드
    /// </summary>
    partial void OnCurrentLanguageCodeChanged(string value)
    {
        if (!string.IsNullOrEmpty(value) && _languageService.GetCurrentLanguage() != value)
        {
            _languageService.ChangeLanguage(value);
            _settingService.Settings.Language = value;
            _ = SaveSettingsAsync();
        }
    }

    /// <summary>
    /// 현재 설정 상태를 settings.json에 저장합니다.
    /// </summary>
    private Task SaveSettingsAsync() => _settingService.SaveAsync();

    [RelayCommand(CanExecute = nameof(CanCheckUpdate))]
    private async Task CheckUpdateAsync()
    {
        IsCheckingUpdate = true;
        UpdateStatusText = GetString("Setting_App_UpdateChecking");

        try
        {
            var result = await _appInfoService.CheckLatestReleaseAsync(CancellationToken.None);
            UpdateStatusText = FormatUpdateStatus(result);
        }
        catch
        {
            UpdateStatusText = GetString("Setting_App_UpdateFailed");
        }
        finally
        {
            IsCheckingUpdate = false;
        }
    }

    [RelayCommand]
    private void OpenGitHub()
    {
        _externalLauncher.OpenUrl(_appInfoService.RepositoryUrl);
    }

    [RelayCommand]
    private void OpenDataFolder()
    {
        _externalLauncher.OpenFolder(_appInfoService.DataFolderPath);
    }

    private bool CanCheckUpdate() => !IsCheckingUpdate;

    partial void OnIsCheckingUpdateChanged(bool value)
    {
        CheckUpdateCommand.NotifyCanExecuteChanged();
    }

    private string FormatUpdateStatus(UpdateCheckResult result)
    {
        string message = GetString(result.MessageKey);
        return result.Status == UpdateCheckStatus.UpdateAvailable && !string.IsNullOrWhiteSpace(result.LatestVersion)
            ? string.Format(message, result.LatestVersion)
            : message;
    }
}
