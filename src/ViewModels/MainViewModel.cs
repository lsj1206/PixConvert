using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using PixConvert.Models;
using PixConvert.Services;
using PixConvert.Services.Interfaces;

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

    /// <summary>정렬 기준, 정렬 방향, 필터 상태를 단독 소유하는 뷰모델</summary>
    public SortFilterViewModel SortFilter { get; }

    // 사이드바 영역 3분할 뷰모델
    public FileInputViewModel FileInput { get; }
    public ConversionViewModel Conversion { get; }
    public ListManagerViewModel ListManager { get; }

    /// <summary>상단 헤더 정보 및 언어 설정 관리 뷰모델</summary>
    public HeaderViewModel Header { get; }

    // 설정창 연동 옵션
    [ObservableProperty] private bool _enableOverlayHover = true;

    /// <summary>
    /// MainViewModel의 새 인스턴스를 초기화하며 필요한 서비스와 서브 뷰모델들을 구성합니다.
    /// </summary>
    public MainViewModel(
        ILogger<MainViewModel> logger,
        ILanguageService languageService,
        IDialogService dialogService,
        ISnackbarService snackbarService,
        SnackbarViewModel snackbarViewModel,
        FileListViewModel fileList,
        SortFilterViewModel sortFilter,
        FileInputViewModel fileInput,
        ConversionViewModel conversion,
        ListManagerViewModel listManager,
        HeaderViewModel header)
        : base(languageService, logger)
    {
        _dialogService = dialogService;
        _snackbarService = snackbarService;
        FileList = fileList;
        Snackbar = snackbarViewModel;
        SortFilter = sortFilter;
        FileInput = fileInput;
        Conversion = conversion;
        ListManager = listManager;
        Header = header;

        // 다른 VM의 상태 변경 요청을 수신 등록
        WeakReferenceMessenger.Default.Register<AppStatusRequestMessage>(this);
    }

    /// <summary>상태 변경 시 UI 알림을 위한 방송을 수행합니다.</summary>
    protected override void OnStatusChanged(AppStatus newStatus)
    {
        // 모든 뷰모델에게 상태 변경을 알림 (동기화)
        WeakReferenceMessenger.Default.Send(new AppStatusChangedMessage(newStatus));
    }

    /// <summary>다른 뷰모델들로부터의 상태 변경 요청을 처리합니다.</summary>
    public void Receive(AppStatusRequestMessage message)
    {
        CurrentStatus = message.NewStatus;
    }
}
