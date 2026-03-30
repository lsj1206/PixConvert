using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Mvvm.ComponentModel;
using PixConvert.Models;
using PixConvert.Services;

namespace PixConvert.ViewModels;

/// <summary>
/// 애플리케이션 전역 설정을 관리하는 뷰모델입니다.
/// </summary>
public partial class AppSettingViewModel : ViewModelBase
{
    private readonly ListManagerViewModel _listManager;

    [ObservableProperty] private string _appVersion = App.Version;

    /// <summary>애플리케이션에서 지원하는 언어 목록</summary>
    public ObservableCollection<LanguageOption> Languages { get; } =
    [
        new() { Display = "English", Code = "en-US" },
        new() { Display = "한국어", Code = "ko-KR" }
    ];

    /// <summary>현재 선택된 언어 옵션</summary>
    [ObservableProperty] private LanguageOption _selectedLanguage;

    /// <summary>파일 삭제 시 확인 메시지 표시 여부 (ListManagerViewModel 연동)</summary>
    public bool ConfirmDeletion
    {
        get => _listManager.ConfirmDeletion;
        set
        {
            if (_listManager.ConfirmDeletion != value)
            {
                _listManager.ConfirmDeletion = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// AppSettingViewModel의 새 인스턴스를 초기화합니다.
    /// </summary>
    public AppSettingViewModel(
        ILanguageService languageService,
        ILogger<AppSettingViewModel> logger,
        ListManagerViewModel listManager)
        : base(languageService, logger)
    {
        _listManager = listManager;

        // 초기 언어 설정 동기화 (현재 적용된 언어 기준)
        var currentLang = _languageService.GetCurrentLanguage();
        SelectedLanguage = Languages.FirstOrDefault(l => l.Code == currentLang) ?? Languages[0];
    }

    /// <summary>
    /// 선택된 언어가 변경되었을 때 호출되는 메서드
    /// </summary>
    partial void OnSelectedLanguageChanged(LanguageOption value)
    {
        if (value != null && _languageService.GetCurrentLanguage() != value.Code)
            _languageService.ChangeLanguage(value.Code);
    }
}
