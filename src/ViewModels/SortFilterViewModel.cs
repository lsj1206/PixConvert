using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using PixConvert.Models;
using PixConvert.Services;

namespace PixConvert.ViewModels;

/// <summary>
/// 파일 목록의 정렬 기준, 정렬 방향, 필터 상태를 단독으로 소유하는 뷰모델입니다.
/// MainViewModel과 SidebarViewModel이 공유하며, 두 VM 간의 직접 결합(Main↔Sidebar)을 제거합니다.
/// </summary>
public partial class SortFilterViewModel : ObservableObject
{
    private readonly FileListViewModel _fileList;
    private readonly ISortingService _sortingService;

    /// <summary>현재 선택된 정렬 기준</summary>
    [ObservableProperty] private SortType _selectedSortType = SortType.AddIndex;

    /// <summary>오름차순 여부</summary>
    [ObservableProperty] private bool _isSortAscending = true;

    /// <summary>불일치 파일만 보기 여부</summary>
    [ObservableProperty] private bool _showMismatchOnly = false;

    public SortFilterViewModel(FileListViewModel fileList, ISortingService sortingService)
    {
        _fileList = fileList;
        _sortingService = sortingService;
    }

    /// <summary>정렬 기준이 변경되면 즉시 정렬 및 필터를 재적용합니다.</summary>
    partial void OnSelectedSortTypeChanged(SortType value) => ApplySortAndFilter();

    /// <summary>정렬 방향이 변경되면 즉시 정렬 및 필터를 재적용합니다.</summary>
    partial void OnIsSortAscendingChanged(bool value) => ApplySortAndFilter();

    /// <summary>필터 조건이 변경되면 즉시 필터를 재적용합니다.</summary>
    partial void OnShowMismatchOnlyChanged(bool value) => ApplyFilter();

    /// <summary>
    /// 컬럼 헤더 클릭 시 호출됩니다.
    /// 동일 기준이면 방향 토글, 다른 기준이면 해당 기준으로 변경 후 오름차순으로 초기화합니다.
    /// </summary>
    public void ToggleOrSetSort(SortType type)
    {
        if (SelectedSortType == type)
            IsSortAscending = !IsSortAscending;
        else
        {
            SelectedSortType = type;
            IsSortAscending = true;
        }
    }

    /// <summary>현재 정렬 설정에 따라 목록을 정렬하고, 이어서 필터를 적용합니다.</summary>
    public void ApplySortAndFilter()
    {
        _fileList.Sorting(_sortingService, SelectedSortType, IsSortAscending);
        ApplyFilter();
    }

    /// <summary>현재 필터 옵션(ShowMismatchOnly)을 CollectionView에 적용합니다.</summary>
    public void ApplyFilter()
    {
        var view = System.Windows.Data.CollectionViewSource.GetDefaultView(_fileList.Items);
        // 불일치 파일만 보기가 활성화된 경우 IsMatch 필터 적용, 아니면 전체 표시
        view.Filter = ShowMismatchOnly
            ? item => item is FileItem fi && fi.IsMismatch
            : null;
        view.Refresh();
    }
}
