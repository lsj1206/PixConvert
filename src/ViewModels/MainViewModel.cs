using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using PixConvert.Models;
using PixConvert.Services;

namespace PixConvert.ViewModels;

/// <summary>
/// 애플리케이션의 메인 쉘(Shell) 역할을 하며, 하위 기능별 뷰모델들을 관리하고 조정하는 최상위 뷰모델입니다.
/// </summary>
public partial class MainViewModel : ViewModelBase, IRecipient<AppStatusRequestMessage>
{
    private readonly IDialogService _dialogService;
    private readonly ISnackbarService _snackbarService;

    /// <summary>하단 알림(스낵바) 제어를 위한 뷰모델</summary>
    public SnackbarViewModel Snackbar { get; }

    /// <summary>파일 목록 데이터 도메인 관리 뷰모델</summary>
    public FileListViewModel FileList { get; }

    /// <summary>좌측 사이드바 액션 관리 뷰모델</summary>
    public SidebarViewModel Sidebar { get; }

    /// <summary>상단 헤더 정보 및 언어 설정 관리 뷰모델</summary>
    public HeaderViewModel Header { get; }


    /// <summary>목록에서 아이템 삭제 시 확인 대화상자를 표시할지 여부</summary>
    [ObservableProperty] private bool _confirmDeletion = true;

    /// 하위 뷰모델로 이동된 명령들을 상위에서도 참조할 수 있도록 브릿지(Bridge) 명령 정의 가능
    /// <summary>선택된 파일들을 목록에서 제거하는 명령</summary>
    public IAsyncRelayCommand DeleteFilesCommand { get; }

    /// <summary>파일 목록을 비우는 명령</summary>
    public IRelayCommand ListClearCommand { get; }

    /// <summary>목록의 순번을 현재 정렬 기준으로 재설정하는 명령</summary>
    public IAsyncRelayCommand ReorderNumberCommand { get; }

    /// <summary>특정 컬럼 클릭 시 해당 컬럼 기준으로 정렬을 수행하는 명령</summary>
    public IRelayCommand<SortType> SortByColumnCommand { get; }

    /// <summary>
    /// MainViewModel의 새 인스턴스를 초기화하며 필요한 서비스와 서브 뷰모델들을 구성합니다.
    /// </summary>
    public MainViewModel(
        ILogger<MainViewModel> logger,
        IDialogService dialogService,
        ISnackbarService snackbarService,
        ILanguageService languageService,
        ISortingService sortingService,
        IFileProcessingService fileProcessingService,
        ILogger<SidebarViewModel> sidebarLogger,
        ILogger<HeaderViewModel> headerLogger,
        SnackbarViewModel snackbarViewModel,
        FileListViewModel fileListViewModel)
        : base(languageService, logger)
    {
        _dialogService = dialogService;
        _snackbarService = snackbarService;
        FileList = fileListViewModel;

        Snackbar = snackbarViewModel;

        // 하위 뷰모델 초기화 (기능별 역할 분담)
        Header = new HeaderViewModel(languageService, headerLogger, FileList);
        Sidebar = new SidebarViewModel(
            sidebarLogger,
            _dialogService,
            _snackbarService,
            languageService,
            fileProcessingService,
            sortingService,
            FileList);

        // 다른 VM의 상태 변경 요청을 수신 등록
        WeakReferenceMessenger.Default.Register<AppStatusRequestMessage>(this);

        // 목록 조작 명령 초기화 (목록 데이터 직접 핸들링)
        DeleteFilesCommand = new AsyncRelayCommand<System.Collections.IList>(DeleteFilesAsync, _ => CurrentStatus == AppStatus.Idle);
        ListClearCommand = new AsyncRelayCommand(ListClearAsync, () => CurrentStatus == AppStatus.Idle);
        ReorderNumberCommand = new AsyncRelayCommand(ReorderNumberAsync, () => CurrentStatus == AppStatus.Idle && !Sidebar.ShowMismatchOnly);
        SortByColumnCommand = new RelayCommand<SortType>(SortByColumn);

        // 언어 변경 시 설정 옵션의 텍스트들을 갱신하기 위한 연동
        Header.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(HeaderViewModel.SelectedLanguage))
            {
                Sidebar.UpdateSortOptions();
            }
        };

        // 설정 값 변경에 따라 명령의 실행 가능 여부(CanExecute) 동적으로 갱신
        Sidebar.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(SidebarViewModel.ShowMismatchOnly))
            {
                ReorderNumberCommand.NotifyCanExecuteChanged();
            }
        };
    }

    /// <summary>상태 변경 시 UI 알림 및 관련 명령들의 실행 가능 여부를 갱신하며, 앱 전체에 변경 사항을 방송합니다.</summary>
    protected override void OnStatusChanged(AppStatus newStatus)
    {
        // 모든 뷰모델에게 상태 변경을 알림 (동기화)
        WeakReferenceMessenger.Default.Send(new AppStatusChangedMessage(newStatus));

        Sidebar.NotifyCommandsStateChanged();
        DeleteFilesCommand.NotifyCanExecuteChanged();
        ListClearCommand.NotifyCanExecuteChanged();
        ReorderNumberCommand.NotifyCanExecuteChanged();
    }

    /// <summary>다른 뷰모델들로부터의 상태 변경 요청을 처리합니다.</summary>
    public void Receive(AppStatusRequestMessage message)
    {
        CurrentStatus = message.NewStatus;
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

    /// <summary>
    /// 선택한 파일 아이템들을 목록에서 제거합니다.
    /// 설정에 따라 삭제 확인 대화상자를 표시할 수 있습니다.
    /// </summary>
    /// <param name="items">제거할 아이템 목록 (IList 타입)</param>
    private async Task DeleteFilesAsync(System.Collections.IList? items)
    {
        if (items == null || items.Count == 0) return;

        CurrentStatus = AppStatus.Processing;
        try
        {
            var itemsToDelete = items.Cast<FileItem>().ToList();
        int count = itemsToDelete.Count;

        // 삭제 전 사용자 확인 (설정 활성화 시)
        if (ConfirmDeletion)
        {
            string message = count == 1 ? GetString("Dlg_Ask_DeleteSingle") : string.Format(GetString("Dlg_Ask_DeleteMulti"), count);
            if (!await _dialogService.ShowConfirmationAsync(message, GetString("Dlg_Title_DeleteConfirm"))) return;
        }

        FileList.RemoveItems(itemsToDelete);
        _snackbarService.Show(string.Format(GetString("Msg_RemoveFile"), count), SnackbarType.Warning);
        }
        finally
        {
            CurrentStatus = AppStatus.Idle;
        }
    }

    /// <summary>
    /// 파일 목록의 모든 항목을 비동기적으로 제거합니다.
    /// </summary>
    private async Task ListClearAsync()
    {
        if (FileList.Items.Count == 0) return;

        // 전체 삭제 전 사용자 확인
        if (await _dialogService.ShowConfirmationAsync(GetString("Dlg_Ask_ClearList"), GetString("Dlg_Title_ClearList")))
        {
            CurrentStatus = AppStatus.Processing;
            try
            {
                FileList.Clear();
                _snackbarService.Show(GetString("Msg_ClearList"), SnackbarType.Success);
            }
            finally
            {
                CurrentStatus = AppStatus.Idle;
            }
        }
    }

    /// <summary>
    /// 현재 목록의 표시 순서에 따라 파일의 인덱스(순번)를 다시 부여합니다.
    /// </summary>
    private async Task ReorderNumberAsync()
    {
        if (FileList.Items.Count == 0) return;

        // 재정렬 전 사용자 확인
        if (await _dialogService.ShowConfirmationAsync(GetString("Dlg_Ask_ReorderIndex"), GetString("Dlg_Title_ReorderIndex")))
        {
            CurrentStatus = AppStatus.Processing;
            try
            {
                FileList.ReorderIndex();
                _snackbarService.Show(GetString("Msg_ReorderIndex"), SnackbarType.Success);
            }
            finally
            {
                CurrentStatus = AppStatus.Idle;
            }
        }
    }

    /// <summary>
    /// 특정 컬럼 헤더 클릭 시 호출되어 정렬 기준을 변경하거나 정렬 방향을 반전시킵니다.
    /// </summary>
    /// <param name="type">정렬 대상 컬럼 타입</param>
    private void SortByColumn(SortType type)
    {
        // 동일한 컬럼 클릭 시 오름차순/내림차순 토글
        if (Sidebar.SelectedSortOption?.Type == type)
            Sidebar.IsSortAscending = !Sidebar.IsSortAscending;
        else
        {
            // 새로운 컬럼 클릭 시 해당 기준으로 정렬 설정
            var option = Sidebar.SortOptions.FirstOrDefault(x => x.Type == type);
            if (option != null)
            {
                Sidebar.SelectedSortOption = option;
                Sidebar.IsSortAscending = true;
            }
        }
    }

    #endregion

}
