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

    /// <summary>
    /// HeaderViewModel의 새 인스턴스를 초기화하며 필요한 서비스를 주입받고 초기 상태를 설정합니다.
    /// </summary>
    /// <param name="languageService">언어 변경 및 시스템 언어 조회 서비스</param>
    /// <param name="fileList">파일 통계 정보를 가져올 목록 뷰모델</param>
    public HeaderViewModel(ILanguageService languageService, FileListViewModel fileList)
    {
        _languageService = languageService;
        _fileList = fileList;

        // 초기 언어 설정: 시스템 언어를 확인하여 기본값 지정
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
    }

    /// <summary>애플리케이션에서 지원하는 언어 목록</summary>
    public ObservableCollection<SettingsViewModel.LanguageOption> Languages { get; } =
    [
        new() { Display = "EN", Code = "en-US" },
        new() { Display = "KR", Code = "ko-KR" }
    ];

    /// <summary>현재 선택된 언어 옵션</summary>
    [ObservableProperty] private SettingsViewModel.LanguageOption selectedLanguage;

    /// <summary>목록의 전체 파일 수 (FileList 위임)</summary>
    public int TotalCount => _fileList.TotalCount;

    /// <summary>미지원(시그니처 미판별) 파일 수 (FileList 위임)</summary>
    public int UnsupportedCount => _fileList.UnsupportedCount;

    /// <summary>언어 선택 변경 시 호출되어 실제 애플리케이션의 언어를 변경합니다.</summary>
    partial void OnSelectedLanguageChanged(SettingsViewModel.LanguageOption value)
    {
        if (value != null)
        {
            _languageService.ChangeLanguage(value.Code);
        }
    }
}
