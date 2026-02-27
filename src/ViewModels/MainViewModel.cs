using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using PixConvert.Models;
using PixConvert.Services;

namespace PixConvert.ViewModels;

/// <summary>
/// 애플리케이션의 메인 쉘(Shell) 역할을 하며, 하위 기능별 뷰모델들을 관리하고 조정하는 최상위 뷰모델입니다.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly ILanguageService _languageService;
    private readonly IDialogService _dialogService;
    private readonly ISnackbarService _snackbarService;
    private readonly ILogger<MainViewModel> _logger;

    /// <summary>하단 알림(스낵바) 제어를 위한 뷰모델</summary>
    public SnackbarViewModel Snackbar { get; }

    /// <summary>파일 목록 데이터 도메인 관리 뷰모델</summary>
    public FileListViewModel FileList { get; } = new();

    /// <summary>환경 설정 및 공유 상태 관리를 위한 뷰모델</summary>
    public SettingsViewModel Settings { get; }

    /// <summary>좌측 사이드바 액션 관리 뷰모델</summary>
    public SidebarViewModel Sidebar { get; }

    /// <summary>상단 헤더 정보 및 언어 설정 관리 뷰모델</summary>
    public HeaderViewModel Header { get; }

    /// <summary>애플리케이션이 현재 작업 중(파일 처리, 변환 등)인지 여부</summary>
    [ObservableProperty]
    private bool isBusy = false;

    // 하위 뷰모델로 이동된 명령들을 상위에서도 참조할 수 있도록 브릿지(Bridge) 명령 정의 가능
    // 또는 View에서 직접 하위 VM의 명령에 바인딩함.
    public IRelayCommand DeleteFilesCommand { get; }
    public IRelayCommand ListClearCommand { get; }
    public IRelayCommand ReorderNumberCommand { get; }
    public IRelayCommand<SortType> SortByColumnCommand { get; }

    public MainViewModel(
        ILogger<MainViewModel> logger,
        IDialogService dialogService,
        ISnackbarService snackbarService,
        ILanguageService languageService,
        ISortingService sortingService,
        IFileProcessingService fileProcessingService,
        ILogger<SidebarViewModel> sidebarLogger,
        SnackbarViewModel snackbarViewModel,
        SettingsViewModel settingsViewModel)
    {
        _logger = logger;
        _dialogService = dialogService;
        _snackbarService = snackbarService;
        _languageService = languageService;

        Snackbar = snackbarViewModel;
        Settings = settingsViewModel;

        // 하위 뷰모델 초기화
        Header = new HeaderViewModel(languageService, FileList);
        Sidebar = new SidebarViewModel(
            sidebarLogger,
            dialogService,
            snackbarService,
            languageService,
            fileProcessingService,
            sortingService,
            FileList,
            Settings,
            () => IsBusy,
            val => IsBusy = val);

        // 목록 조작 명령 (FileList 데이터 직접 조작이므로 Main에서 서비스 주입받아 수행)
        DeleteFilesCommand = new RelayCommand<System.Collections.IList>(DeleteFiles, _ => !IsBusy);
        ListClearCommand = new AsyncRelayCommand(ListClearAsync, () => !IsBusy);
        ReorderNumberCommand = new RelayCommand(ReorderNumber, () => !IsBusy && !Settings.ShowMismatchOnly);
        SortByColumnCommand = new RelayCommand<SortType>(SortByColumn);

        // 언어 변경 시 정렬 옵션 갱신을 위해 Header와 Settings 간 연동 (간접)
        Header.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(HeaderViewModel.SelectedLanguage))
            {
                Settings.UpdateSortOptions();
            }
        };

        // 설정 변경에 따른 명령 상태 갱신
        Settings.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(SettingsViewModel.ShowMismatchOnly))
            {
                ReorderNumberCommand.NotifyCanExecuteChanged();
            }
        };
    }

    partial void OnIsBusyChanged(bool value)
    {
        Sidebar.NotifyCommandsStateChanged();
        DeleteFilesCommand.NotifyCanExecuteChanged();
        ListClearCommand.NotifyCanExecuteChanged();
        ReorderNumberCommand.NotifyCanExecuteChanged();
    }

    /// <summary>탐색기 드롭 이벤트 처리용 브릿지 메서드</summary>
    public void DropFiles(string[] paths)
    {
        if (paths != null && paths.Length > 0)
        {
            _ = Sidebar.ProcessFiles(paths);
        }
    }

    #region List Manipulation (MainVM retains these for UI delegate/bridge)

    private async void DeleteFiles(System.Collections.IList? items)
    {
        if (items == null || items.Count == 0) return;

        var itemsToDelete = items.Cast<FileItem>().ToList();
        int count = itemsToDelete.Count;

        if (Settings.ConfirmDeletion)
        {
            string message = count == 1 ? GetString("Dlg_Ask_DeleteSingle") : string.Format(GetString("Dlg_Ask_DeleteMulti"), count);
            if (!await _dialogService.ShowConfirmationAsync(message, GetString("Dlg_Title_DeleteConfirm"))) return;
        }

        FileList.RemoveItems(itemsToDelete);
        _snackbarService.Show(string.Format(GetString("Msg_RemoveFile"), count), SnackbarType.Warning);
    }

    private async Task ListClearAsync()
    {
        if (FileList.Items.Count == 0) return;
        if (await _dialogService.ShowConfirmationAsync(GetString("Dlg_Ask_ClearList"), GetString("Dlg_Title_ClearList")))
        {
            FileList.Clear();
            _snackbarService.Show(GetString("Msg_ClearList"), SnackbarType.Success);
        }
    }

    private async void ReorderNumber()
    {
        if (FileList.Items.Count == 0) return;
        if (await _dialogService.ShowConfirmationAsync(GetString("Dlg_Ask_ReorderIndex"), GetString("Dlg_Title_ReorderIndex")))
        {
            FileList.ReorderIndex();
            _snackbarService.Show(GetString("Msg_ReorderIndex"), SnackbarType.Success);
        }
    }

    private void SortByColumn(SortType type)
    {
        if (Settings.SelectedSortOption?.Type == type)
            Settings.IsSortAscending = !Settings.IsSortAscending;
        else
        {
            var option = Settings.SortOptions.FirstOrDefault(x => x.Type == type);
            if (option != null)
            {
                Settings.SelectedSortOption = option;
                Settings.IsSortAscending = true;
            }
        }
    }

    #endregion

    private string GetString(string key) => _languageService.GetString(key);
}
