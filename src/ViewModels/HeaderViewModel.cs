using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using PixConvert.Services;
using PixConvert.Models;

namespace PixConvert.ViewModels;

/// <summary>
/// 상단 헤더의 상태 정보(합계, 미지원) 및 언어 설정을 관리하는 뷰모델입니다.
/// </summary>
public partial class HeaderViewModel : ObservableObject
{
    private readonly ILanguageService _languageService;
    private readonly FileListViewModel _fileList;

    public HeaderViewModel(ILanguageService languageService, FileListViewModel fileList)
    {
        _languageService = languageService;
        _fileList = fileList;

        // 초기 언어 설정
        var systemLang = _languageService.GetSystemLanguage();
        SelectedLanguage = Languages.FirstOrDefault(l => l.Code == systemLang) ?? Languages[0];

        // FileListViewModel의 속성 변경 알림을 구독하여 자신의 속성도 알림
        _fileList.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(FileListViewModel.TotalCount))
                OnPropertyChanged(nameof(TotalCount));
            else if (e.PropertyName == nameof(FileListViewModel.UnsupportedCount))
                OnPropertyChanged(nameof(UnsupportedCount));
        };
    }

    public ObservableCollection<SettingsViewModel.LanguageOption> Languages { get; } =
    [
        new() { Display = "EN", Code = "en-US" },
        new() { Display = "KR", Code = "ko-KR" }
    ];

    [ObservableProperty] private SettingsViewModel.LanguageOption selectedLanguage;

    /// <summary>목록의 전체 파일 수</summary>
    public int TotalCount => _fileList.TotalCount;

    /// <summary>미지원(시그니처 미판별) 파일 수</summary>
    public int UnsupportedCount => _fileList.UnsupportedCount;

    partial void OnSelectedLanguageChanged(SettingsViewModel.LanguageOption value)
    {
        if (value != null)
        {
            _languageService.ChangeLanguage(value.Code);
        }
    }
}
