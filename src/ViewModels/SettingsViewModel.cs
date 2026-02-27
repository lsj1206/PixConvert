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

    /// <summary>
    /// SettingsViewModel의 새 인스턴스를 초기화하며 언어 서비스를 주입받고 정렬 옵션을 구성합니다.
    /// </summary>
    public SettingsViewModel(ILanguageService languageService)
    {
        _languageService = languageService;

        // 정렬 옵션 초기화: 로컬라이징된 문자열로 목록 구성
        UpdateSortOptions();
        SelectedSortOption = SortOptions[0];
    }

    /// <summary>언어 선택을 위한 옵션 정보를 담는 클래스</summary>
    public class LanguageOption
    {
        /// <summary>화면에 표시될 이름 (예: EN, KR)</summary>
        public string Display { get; set; } = string.Empty;
        /// <summary>언어 코드 (예: en-US, ko-KR)</summary>
        public string Code { get; set; } = string.Empty;
    }


    /// <summary>지원되는 정렬 컬럼 및 옵션 목록</summary>
    public ObservableCollection<SortOption> SortOptions { get; } = [];

    /// <summary>현재 선택된 정렬 기준</summary>
    [ObservableProperty] private SortOption selectedSortOption;

    /// <summary>오름차순/내림차순 정렬 여부</summary>
    [ObservableProperty] private bool isSortAscending = true;

    /// <summary>확장자-시그니처 불일치 파일만 필터링하여 보여줄지 여부</summary>
    [ObservableProperty] private bool showMismatchOnly = false;

    /// <summary>목록에서 아이템 삭제 시 확인 대화상자를 표시할지 여부</summary>
    [ObservableProperty] private bool confirmDeletion = true;


    /// <summary>
    /// 현재 언어 설정에 맞게 정렬 옵션의 표시 텍스트를 최신화합니다.
    /// </summary>
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
        SortOptions.Add(new SortOption { Display = GetString("List_ExtensionToPath"), Type = SortType.Extension });
        SortOptions.Add(new SortOption { Display = GetString("List_FileSignature"), Type = SortType.Signature });

        // 기존에 선택되어 있던 타입을 유지하거나 기본값(순번)으로 설정
        SelectedSortOption = SortOptions.FirstOrDefault(x => x.Type == currentType) ?? SortOptions[0];
    }

    private string GetString(string key) => _languageService.GetString(key);
}
