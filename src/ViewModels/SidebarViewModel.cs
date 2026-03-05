using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using PixConvert.Models;
using PixConvert.Services;
using PixConvert.Services.Interfaces;

namespace PixConvert.ViewModels;

/// <summary>
/// 좌측 사이드바의 액션(파일 추가, 변환, 설정 오픈 등)을 관리하는 뷰모델입니다.
/// </summary>
public partial class SidebarViewModel : ViewModelBase
{
    private const int MaxItemCount = 10000;

    private readonly IDialogService _dialogService;
    private readonly ISnackbarService _snackbarService;
    private readonly IFileAnalyzerService _fileAnalyzerService;
    private readonly ISortingService _sortingService;
    private readonly IAppConfigService _configService;
    private readonly ILoggerFactory _loggerFactory;

    // 타 뷰모델 참조
    private readonly FileListViewModel _fileList;

    /// <summary>현재 선택된 정렬 기준</summary>
    [ObservableProperty] private SortType _selectedSortType = SortType.AddIndex;

    /// <summary>오름차순/내림차순 정렬 여부</summary>
    [ObservableProperty] private bool _isSortAscending = true;

    /// <summary>확장자-시그니처 불일치 파일만 필터링하여 보여줄지 여부</summary>
    [ObservableProperty] private bool _showMismatchOnly = false;

    /// <summary>파일들을 개별적으로 선택하여 목록에 추가하는 명령</summary>
    public IAsyncRelayCommand AddFilesCommand { get; }
    /// <summary>폴더를 선택하여 내부의 파일들을 목록에 추가하는 명령</summary>
    public IAsyncRelayCommand AddFolderCommand { get; }
    /// <summary>변환 설정 대화상자를 여는 명령</summary>
    public IAsyncRelayCommand OpenConvertSettingCommand { get; }
    /// <summary>목록에 있는 파일들의 변환 작업을 시작하는 비동기 명령</summary>
    public IAsyncRelayCommand ConvertFilesCommand { get; }

    /// <summary>
    /// SidebarViewModel의 새 인스턴스를 초기화하며 필요한 서비스와 상태 제어 델리게이트를 주입받습니다.
    /// </summary>
    public SidebarViewModel(
        ILogger<SidebarViewModel> logger,
        IDialogService dialogService,
        ISnackbarService snackbarService,
        ILanguageService languageService,
        IFileAnalyzerService fileAnalyzerService,
        ISortingService sortingService,
        IAppConfigService configService,
        ILoggerFactory loggerFactory,
        FileListViewModel fileList)
        : base(languageService, logger)
    {
        _dialogService = dialogService;
        _snackbarService = snackbarService;
        _fileAnalyzerService = fileAnalyzerService;
        _sortingService = sortingService;
        _configService = configService;
        _loggerFactory = loggerFactory;
        _fileList = fileList;

        // 명령 초기화: Busy 상태에 따른 실행 가능 여부 설정
        AddFilesCommand = new AsyncRelayCommand(AddFilesAsync, () => CurrentStatus == AppStatus.Idle);
        AddFolderCommand = new AsyncRelayCommand(AddFolderAsync, () => CurrentStatus == AppStatus.Idle);
        OpenConvertSettingCommand = new AsyncRelayCommand(OpenConvertSettingAsync, () => CurrentStatus == AppStatus.Idle);
        ConvertFilesCommand = new AsyncRelayCommand(ConvertFilesAsync, () => CurrentStatus == AppStatus.Idle);

    }

    /// <summary>상태 변경 시 사이드바 명령들의 실행 가능 여부를 자동으로 갱신합니다.</summary>
    protected override void OnStatusChanged(AppStatus newStatus)
    {
        NotifyCommandsStateChanged();
    }

    /// <summary>필터 또는 정렬 값이 변경될 때 UI를 갱신합니다.</summary>
    partial void OnSelectedSortTypeChanged(SortType value) => SortFiles();
    partial void OnIsSortAscendingChanged(bool value) => SortFiles();
    partial void OnShowMismatchOnlyChanged(bool value) => ApplyFilter();

    /// <summary>외부 상태 변경에 따라 사이드바 명령들의 실행 가능 여부를 강제로 갱신합니다.</summary>
    public void NotifyCommandsStateChanged()
    {
        AddFilesCommand.NotifyCanExecuteChanged();
        AddFolderCommand.NotifyCanExecuteChanged();
        OpenConvertSettingCommand.NotifyCanExecuteChanged();
        ConvertFilesCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// 파일 열기 대화상자를 통해 사용자가 선택한 파일들을 목록에 추가합니다.
    /// </summary>
    private async Task AddFilesAsync()
    {
        var dialog = new OpenFileDialog { Multiselect = true, Title = GetString("Dlg_Title_AddFile") };
        if (dialog.ShowDialog() == true) await ProcessFiles(dialog.FileNames);
    }

    /// <summary>
    /// 폴더 선택 대화상자를 통해 사용자가 선택한 폴더 내의 파일들을 목록에 추가합니다.
    /// </summary>
    private async Task AddFolderAsync()
    {
        var dialog = new OpenFolderDialog { Multiselect = true, Title = GetString("Dlg_Title_AddFolder") };
        if (dialog.ShowDialog() == true) await ProcessFiles(dialog.FolderNames);
    }

    /// <summary>
    /// 실제 경로(파일 또는 폴더)들을 바탕으로 파일 정보를 추출하고 목록에 추가하는 핵심 비즈니스 로직입니다.
    /// </summary>
    /// <param name="paths">추가할 파일 또는 폴더의 절대 경로 컬렉션</param>
    public async Task ProcessFiles(IEnumerable<string> paths)
    {
        RequestStatus(AppStatus.FileAdd);
        try
        {
            var pathList = paths.ToList();
            _logger.LogInformation(GetString("Log_Main_ProcessStart"), pathList.Count);
            _snackbarService.ShowProgress(GetString("Msg_LoadingFile"));

            // 진행률 보고를 위한 콜백 설정
            var progress = new Progress<FileProcessingProgress>(p =>
            {
                _snackbarService.UpdateProgress(string.Format(GetString("Msg_LoadingFileProgress"), p.CurrentIndex, p.TotalCount));
            });

            // 서비스 엔진을 통해 파일 스캔 및 데이터 객체 생성
            var result = await _fileAnalyzerService.ProcessPathsAsync(
                paths,
                MaxItemCount,
                _fileList.Items.Count,
                progress);

            // 파일 한도 초과 등에 따른 예외 알림
            if (result.SuccessCount == 0 && result.IgnoredCount > 0)
            {
                var msg = string.Format(GetString("Msg_MaxItemExceeded"), MaxItemCount, _fileList.Items.Count, result.IgnoredCount);
                _snackbarService.Show(msg, SnackbarType.Error);
                return;
            }

            // 스캔 성공 시 목록 뷰모델에 실제 삽입
            if (result.SuccessCount > 0)
            {
                AddFilesToList(result.NewItems);
            }
        }
        catch (Exception ex)
        {
            _snackbarService.Show(string.Format(GetString("Msg_Error_Occurred"), ex.Message), SnackbarType.Error);
        }
        finally
        {
            RequestStatus(AppStatus.Idle);
        }
    }

    /// <summary>
    /// 처리된 파일 아이템 리스트를 실제 FileListViewModel 데이터 소스에 삽입하고 결과에 따라 스낵바 알림을 표시합니다.
    /// </summary>
    /// <param name="items">추가할 파일 아이템 리스트</param>
    private void AddFilesToList(List<FileItem> items)
    {
        int totalCount = items.Count;
        int successCount = _fileList.AddRange(items);
        SortFiles();

        // 추가 결과에 따른 사용자 피드백 (이미 존재, 일부 성공, 전체 성공 등)
        if (successCount == 0 && totalCount > 0)
            _snackbarService.Show(GetString("Msg_AlreadyExists"), SnackbarType.Error);
        else if (successCount < totalCount)
            _snackbarService.Show(string.Format(GetString("Msg_AddFilePartial"), totalCount, successCount), SnackbarType.Warning);
        else
            _snackbarService.Show(string.Format(GetString("Msg_AddFile"), successCount), SnackbarType.Success);
    }

    /// <summary>
    /// 변환 설정 다이얼로그를 표시하며, 사용자가 설정을 변경하고 확인을 누르면 설정을 동기화합니다.
    /// </summary>
    private async Task OpenConvertSettingAsync()
    {
        // 1. 설정 창을 열 때 로컬 파일에서 설정을 새로 로드
        await _configService.LoadAsync();

        // 2. 뷰모델 인스턴스화 (DI를 통해 필요한 서비스 주입)
        var vm = new ConvertSettingViewModel(_languageService, _loggerFactory.CreateLogger<ConvertSettingViewModel>(), _configService);

        // 3. 뷰 생성 및 DataContext 맵핑
        var view = new PixConvert.Views.Controls.ConvertSettingDialog { DataContext = vm };

        bool isSaved = await _dialogService.ShowCustomDialogAsync(
            view,
            GetString("Btn_ConvertSetting"),
            GetString("Dlg_Confirm"),
            GetString("Dlg_Cancel"));

        // 4. 창이 닫히면(Implicit Save) 또는 명시적으로 저장
        if (isSaved)
        {
            vm.SyncToSettings();
        }

        // 설정창이 종료될 때 자동 저장
        await _configService.SaveAsync();
    }

    /// <summary>
    /// 목록에 있는 파일들에 대해 실제 변환 프로세스를 실행합니다.
    /// 비즈니스 로직에 따라 개별 파일의 상태와 진행률을 업데이트합니다.
    /// </summary>
    private async Task ConvertFilesAsync()
    {
        if (_fileList.Items.Count == 0) return;

        RequestStatus(AppStatus.Converting); // 애플리케이션을 변환 중 상태로 전환
        try
        {
            foreach (var item in _fileList.Items)
            {
                // 미지원 파일은 건너뜀
                if (item.Status == FileConvertStatus.Unsupported) continue;

                item.Status = FileConvertStatus.Processing;
                item.Progress = 0;

                // 시뮬레이션: 실제 변환 로직이 들어갈 자리
                for (int i = 0; i <= 100; i += 20)
                {
                    item.Progress = i;
                    await Task.Delay(100);
                }

                item.Status = FileConvertStatus.Success;
            }
        }
        finally
        {
            RequestStatus(AppStatus.Idle); // 작업 완료 후 대기 상태로 복구
        }
    }

    /// <summary>
    /// 현재 설정된 옵션에 따라 목록을 정렬합니다.
    /// </summary>
    private void SortFiles()
    {
        _fileList.Sorting(_sortingService, SelectedSortType, IsSortAscending);
        ApplyFilter();
    }

    /// <summary>
    /// 현재 설정된 필터 옵션(예: 불일치 파일만 보기)을 목록 뷰에 적용합니다.
    /// </summary>
    private void ApplyFilter()
    {
        var view = System.Windows.Data.CollectionViewSource.GetDefaultView(_fileList.Items);
        if (ShowMismatchOnly)
        {
            view.Filter = item => item is FileItem fileItem && fileItem.IsMismatch;
        }
        else
        {
            view.Filter = null;
        }
        view.Refresh();
    }

}
