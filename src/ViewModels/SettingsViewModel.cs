using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using PixConvert.Services;
using PixConvert.Models;

namespace PixConvert.ViewModels;

/// <summary>
/// 애플리케이션의 환경 설정 및 공유 상태를 관리하는 뷰모델입니다.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ILanguageService _languageService;

    public SettingsViewModel(ILanguageService languageService)
    {
        _languageService = languageService;

        // 초기 언어 설정
        var systemLang = _languageService.GetSystemLanguage();
        SelectedLanguage = Languages.FirstOrDefault(l => l.Code == systemLang) ?? Languages[0];

        // 정렬 옵션 초기화
        UpdateSortOptions();
        SelectedSortOption = SortOptions[0];
    }

    public class LanguageOption
    {
        public string Display { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }


    public ObservableCollection<LanguageOption> Languages { get; } =
    [
        new() { Display = "EN", Code = "en-US" },
        new() { Display = "KR", Code = "ko-KR" }
    ];

    public ObservableCollection<SortOption> SortOptions { get; } = [];

    [ObservableProperty] private LanguageOption selectedLanguage;
    [ObservableProperty] private SortOption selectedSortOption;
    [ObservableProperty] private bool isSortAscending = true;
    [ObservableProperty] private bool confirmDeletion = true;
    [ObservableProperty] private bool showExtension = true;

    partial void OnSelectedLanguageChanged(LanguageOption value)
    {
        if (value != null)
        {
            _languageService.ChangeLanguage(value.Code);
            UpdateSortOptions();
        }
    }

    public void UpdateSortOptions()
    {
        var currentType = SelectedSortOption?.Type ?? SortType.AddIndex;

        SortOptions.Clear();
        SortOptions.Add(new SortOption { Display = GetString("Sort_Index"), Type = SortType.AddIndex });
        SortOptions.Add(new SortOption { Display = GetString("Sort_NameIndex"), Type = SortType.NameIndex });
        SortOptions.Add(new SortOption { Display = GetString("Sort_NamePath"), Type = SortType.NamePath });
        SortOptions.Add(new SortOption { Display = GetString("Sort_PathIndex"), Type = SortType.PathIndex });
        SortOptions.Add(new SortOption { Display = GetString("Sort_PathName"), Type = SortType.PathName });
        SortOptions.Add(new SortOption { Display = GetString("Sort_Size"), Type = SortType.Size });
        SortOptions.Add(new SortOption { Display = GetString("Sort_CreatedDate"), Type = SortType.CreatedDate });
        SortOptions.Add(new SortOption { Display = GetString("Sort_ModifiedDate"), Type = SortType.ModifiedDate });

        SelectedSortOption = SortOptions.FirstOrDefault(x => x.Type == currentType) ?? SortOptions[0];
    }

    private string GetString(string key) => _languageService.GetString(key);
}
