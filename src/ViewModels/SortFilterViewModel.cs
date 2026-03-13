using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using PixConvert.Models;
using PixConvert.Services;
using PixConvert.Services.Interfaces;

namespace PixConvert.ViewModels;

/// <summary>
/// 파일 목록의 정렬 기준, 정렬 방향, 필터 상태를 단독으로 소유하는 뷰모델입니다.
/// </summary>
public partial class SortFilterViewModel : ViewModelBase
{
    private readonly FileListViewModel _fileList;
    private readonly ISortingService _sortingService;
    private readonly IDialogService _dialogService;
    private readonly ISnackbarService _snackbarService;

    /// <summary>현재 선택된 정렬 기준</summary>
    [ObservableProperty] private SortType _selectedSortType = SortType.AddIndex;

    /// <summary>오름차순 여부</summary>
    [ObservableProperty] private bool _isSortAscending = true;

    /// <summary>불일치 파일만 보기 여부</summary>
    [ObservableProperty] private bool _showMismatchOnly = false;

    /// <summary>목록의 순번을 현재 정렬 기준으로 재설정하는 명령</summary>
    public IAsyncRelayCommand ReorderNumberCommand { get; }

    /// <summary>특정 컬럼 클릭 시 정렬을 수행하는 명령</summary>
    public IRelayCommand<SortType> SortByColumnCommand { get; }

    public SortFilterViewModel(
        ILogger<SortFilterViewModel> logger,
        ILanguageService languageService,
        FileListViewModel fileList,
        ISortingService sortingService,
        IDialogService dialogService,
        ISnackbarService snackbarService)
        : base(languageService, logger)
    {
        _fileList = fileList;
        _sortingService = sortingService;
        _dialogService = dialogService;
        _snackbarService = snackbarService;

        ReorderNumberCommand = new AsyncRelayCommand(ReorderNumberAsync, () => CurrentStatus == AppStatus.Idle && !ShowMismatchOnly);
        SortByColumnCommand = new RelayCommand<SortType>(ToggleOrSetSort);
    }

    /// <summary>정렬 기준이 변경되면 즉시 정렬 및 필터를 재적용합니다.</summary>
    partial void OnSelectedSortTypeChanged(SortType value) => ApplySortAndFilter();

    /// <summary>정렬 방향이 변경되면 즉시 정렬 및 필터를 재적용합니다.</summary>
    partial void OnIsSortAscendingChanged(bool value) => ApplySortAndFilter();

    /// <summary>필터 조건이 변경되면 즉시 필터를 재적용하며, 재정렬 명령의 실행 가능 여부를 갱신합니다.</summary>
    partial void OnShowMismatchOnlyChanged(bool value)
    {
        ApplyFilter();
        ReorderNumberCommand.NotifyCanExecuteChanged();
    }

    protected override void OnStatusChanged(AppStatus newStatus)
    {
        ReorderNumberCommand.NotifyCanExecuteChanged();
    }

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
        view.Filter = ShowMismatchOnly
            ? item => item is FileItem fi && fi.IsMismatch
            : null;
        view.Refresh();
    }

    /// <summary>
    /// 현재 목록의 표시 순서에 따라 파일의 인덱스(순번)를 다시 부여합니다.
    /// </summary>
    private async Task ReorderNumberAsync()
    {
        if (_fileList.Items.Count == 0) return;

        if (await _dialogService.ShowConfirmationAsync(GetString("Dlg_Ask_ReorderIndex"), GetString("Dlg_Title_ReorderIndex")))
        {
            RequestStatus(AppStatus.Processing);
            try
            {
                _fileList.ReorderIndex();
                _snackbarService.Show(GetString("Msg_ReorderIndex"), SnackbarType.Success);
            }
            finally
            {
                RequestStatus(AppStatus.Idle);
            }
        }
    }
}
