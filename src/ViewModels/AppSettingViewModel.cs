using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using PixConvert.Services;

namespace PixConvert.ViewModels;

/// <summary>
/// 애플리케이션 전역 설정을 관리하는 뷰모델입니다.
/// </summary>
public partial class AppSettingViewModel : ViewModelBase
{
    [ObservableProperty] private bool _enableNotification = true;
    [ObservableProperty] private string _currentTheme = "Light";
    [ObservableProperty] private string _appVersion = App.Version;
    [ObservableProperty] private string _developerName = "PixConvert Team";

    public AppSettingViewModel(ILanguageService languageService, ILogger<AppSettingViewModel> logger)
        : base(languageService, logger)
    {
    }
}
