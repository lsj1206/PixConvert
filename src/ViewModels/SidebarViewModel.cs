using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
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
    private readonly IPresetService _presetService;
    private readonly ILoggerFactory _loggerFactory;

    // 타 뷰모델 참조
    private readonly FileListViewModel _fileList;

    /// <summary>현재 선택된 정렬 기준</summary>
    [ObservableProperty] private SortType _selectedSortType = SortType.AddIndex;

    /// <summary>오름차순/내림차순 정렬 여부</summary>
    [ObservableProperty] private bool _isSortAscending = true;

    /// <summary>필터 적용 기준: 불일치 파일들만 표시할지 여부</summary>
    [ObservableProperty] private bool _showMismatchOnly = false;

    /// <summary>현재 변환 작업의 전체 진행률(0~100)을 나타냅니다.</summary>
    [ObservableProperty] private int _convertProgressPercent;

    /// <summary>파일들을 개별적으로 선택하여 목록에 추가하는 명령</summary>
    public IAsyncRelayCommand AddFilesCommand { get; }
    /// <summary>폴더를 선택하여 내부의 파일들을 목록에 추가하는 명령</summary>
    public IAsyncRelayCommand AddFolderCommand { get; }
    /// <summary>변환 설정 대화상자를 여는 명령</summary>
    public IAsyncRelayCommand OpenConvertSettingCommand { get; }
    /// <summary>목록에 추가된 파일들을 실제 변환 엔진을 통해 처리하는 명령</summary>
    public IAsyncRelayCommand ConvertFilesCommand { get; }

    /// <summary>릪이 설정 모드에서 선택준인 확장자 태그 목록</summary>
    public System.Collections.ObjectModel.ObservableCollection<FormatTagViewModel> ExtensionTags { get; } = new();

    /// <summary>릪이 설정 모드에서 미지원 파일 제거 여부</summary>
    [ObservableProperty] private bool _removeUnsupported;


    /// <summary>목록 관리 모드 진입 명령</summary>
    public IRelayCommand EnterListManagerCommand { get; }
    /// <summary>목록 관리 모드 이탈 명령</summary>
    public IRelayCommand ExitListManagerCommand { get; }
    /// <summary>선택된 확장자 + 미지원 파일 일괄 제거</summary>
    public IRelayCommand RemoveSelectedCommand { get; }
    /// <summary>목록 전부 제거 (확인 후 실행)</summary>
    public IAsyncRelayCommand RemoveAllListCommand { get; }

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
        IPresetService presetService,
        ILoggerFactory loggerFactory,
        FileListViewModel fileList)
        : base(languageService, logger)
    {
        _dialogService = dialogService;
        _snackbarService = snackbarService;
        _fileAnalyzerService = fileAnalyzerService;
        _sortingService = sortingService;
        _presetService = presetService;
        _loggerFactory = loggerFactory;
        _fileList = fileList;

        // 명령 초기화: Busy 상태에 따른 실행 가능 여부 설정
        AddFilesCommand = new AsyncRelayCommand(AddFilesAsync, () => CurrentStatus == AppStatus.Idle);
        AddFolderCommand = new AsyncRelayCommand(AddFolderAsync, () => CurrentStatus == AppStatus.Idle);
        OpenConvertSettingCommand = new AsyncRelayCommand(OpenConvertSettingAsync, () => CurrentStatus == AppStatus.Idle);
        ConvertFilesCommand = new AsyncRelayCommand(ConvertFilesAsync, () => CurrentStatus == AppStatus.Idle);

        EnterListManagerCommand = new RelayCommand(EnterListManager, () => CurrentStatus == AppStatus.Idle);
        ExitListManagerCommand = new RelayCommand(ExitListManager, () => CurrentStatus == AppStatus.ListManager);
        RemoveSelectedCommand = new RelayCommand(RemoveSelected, () => CurrentStatus == AppStatus.ListManager);
        RemoveAllListCommand = new AsyncRelayCommand(RemoveAllListAsync, () => CurrentStatus == AppStatus.ListManager);
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

        EnterListManagerCommand.NotifyCanExecuteChanged();
        ExitListManagerCommand.NotifyCanExecuteChanged();
        RemoveSelectedCommand.NotifyCanExecuteChanged();
        RemoveAllListCommand.NotifyCanExecuteChanged();
    }

    /// <summary>목록 설정 모드로 진입합니다. 현재 파일 목록에서 확장자를 불러와 태그를 초기화합니다.</summary>
    private void EnterListManager()
    {
        // 확장자 태그 초기화: 현재 파일 목록에 존재하는 고유 확장자 수집
        ExtensionTags.Clear();
        RemoveUnsupported = false;

        var signatures = _fileList.Items
            .Where(i => i.Status != FileConvertStatus.Unsupported && i.FileSignature != "-")
            .Select(i => i.FileSignature)
            .Distinct()
            .OrderBy(s => s);

        foreach (var sig in signatures)
            ExtensionTags.Add(new FormatTagViewModel(sig));

        RequestStatus(AppStatus.ListManager);
    }

    /// <summary>목록 설정 모드에서 이탈하여 Idle 상태로 복귀합니다.</summary>
    private void ExitListManager() => RequestStatus(AppStatus.Idle);

    /// <summary>선택된 확장자 태그 및 미지원 파일 체크 결과를 일괄 제거합니다.</summary>
    private void RemoveSelected()
    {
        var extsToRemove = ExtensionTags
            .Where(t => t.IsSelected)
            .Select(t => t.Format)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var itemsToRemove = _fileList.Items.Where(i =>
            (RemoveUnsupported && i.Status == FileConvertStatus.Unsupported) ||
            (i.FileSignature != "-" && extsToRemove.Contains(i.FileSignature))
        ).ToList();

        if (itemsToRemove.Count > 0)
        {
            _fileList.RemoveItems(itemsToRemove);
            _snackbarService.Show(string.Format(GetString("Msg_RemoveFile"), itemsToRemove.Count), SnackbarType.Success);
        }

        // 제거 후 태그 재초기화 (삭제된 파일의 확장자 태그 정리)
        ExtensionTags.Clear();
        RemoveUnsupported = false;

        var signatures = _fileList.Items
            .Where(i => i.Status != FileConvertStatus.Unsupported && i.FileSignature != "-")
            .Select(i => i.FileSignature)
            .Distinct()
            .OrderBy(s => s);

        foreach (var sig in signatures)
            ExtensionTags.Add(new FormatTagViewModel(sig));
    }

    /// <summary>사용자 확인 후 목록 전체를 비웁니다.</summary>
    private async Task RemoveAllListAsync()
    {
        if (_fileList.Items.Count == 0) return;

        bool confirmed = await _dialogService.ShowConfirmationAsync(
            GetString("Dlg_Ask_ClearList"),
            GetString("Dlg_Title_ClearList"));

        if (confirmed)
        {
            _fileList.Clear();
            // 목록이 비워지면 태그도 초기화
            ExtensionTags.Clear();
            RemoveUnsupported = false;
            _snackbarService.Show(GetString("Msg_ClearList"), SnackbarType.Success);
        }
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
            _logger.LogInformation(GetString("Log_Sidebar_ProcessStart"), pathList.Count);
            _snackbarService.ShowProgress(GetString("Msg_LoadingFile"));

            // 진행률 보고를 위한 콜백 설정
            var progress = new Progress<FileProcessingProgress>(p =>
            {
                _snackbarService.UpdateProgress(string.Format(GetString("Msg_LoadingFileProgress"), p.CurrentIndex, p.TotalCount));
            });

            // 1. 서비스 엔진을 통해 파일 스캔 및 데이터 객체 생성 (기존 HashSet 재사용)
            var result = await _fileAnalyzerService.ProcessPathsAsync(
                paths,
                MaxItemCount,
                _fileList.Items.Count,
                _fileList.PathSet,
                progress);

            // 2. 추가 가능한 파일이 없는 경우 (상황별 명확한 메시지)
            if (result.SuccessCount == 0 && (result.IgnoredCount > 0 || result.DuplicateCount > 0))
            {
                if (result.IgnoredCount > 0)
                {
                    // 10000개 제한에 걸린 경우 (기존 존재 여부와 무관하게 더 추가 불가)
                    _snackbarService.Show(GetString("Msg_LimitReached"), SnackbarType.Error);
                }
                else
                {
                    // 수량은 남았으나 입력된 파일이 모두 이미 존재하는 경우
                    _snackbarService.Show(GetString("Msg_NoNewFiles"), SnackbarType.Error);
                }
                return;
            }

            // 3. 스캔 성공 시 목록 뷰모델에 실제 삽입 및 결과 안내 가독성 개편
            if (result.SuccessCount > 0)
            {
                AddFilesToList(result.NewItems);
                
                // 알림 우선순위: 한도 초과(부분추가) > 중복 제외 > 일반 성공
                if (result.IgnoredCount > 0)
                {
                    // 부분 추가 (한도 제한으로 일부 누락)
                    _snackbarService.Show(string.Format(GetString("Msg_AddWithLimit"), result.SuccessCount, result.IgnoredCount), SnackbarType.Warning);
                }
                else if (result.DuplicateCount > 0)
                {
                    // 일부 중복 제외
                    _snackbarService.Show(string.Format(GetString("Msg_AddWithDuplicate"), result.SuccessCount, result.DuplicateCount), SnackbarType.Warning);
                }
                else
                {
                    // 깔끔하게 모두 성공
                    _snackbarService.Show(string.Format(GetString("Msg_AddFile"), result.SuccessCount), SnackbarType.Success);
                }
            }
        }
        catch (Exception ex)
        {
            // 운영 로그 파일에 구조화 로그를 남기고, 사용자에게도 스낵바로 피드백 제공
            _logger.LogError(ex, GetString("Log_Sidebar_ProcessError"));
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
    private int AddFilesToList(List<FileItem> items)
    {
        int successCount = _fileList.AddRange(items);
        SortFiles();
        return successCount;
    }

    /// <summary>
    /// 변환 설정 다이얼로그를 표시하며, 사용자가 설정을 변경하고 확인을 누르면 설정을 동기화합니다.
    /// </summary>
    private async Task OpenConvertSettingAsync()
    {
        // 1. 설정 창을 열 때 로컬 파일에서 설정을 새로 로드
        await _presetService.LoadAsync();

        // 2. 뷰모델 인스턴스화 (DI를 통해 필요한 서비스 주입)
        var vm = new ConvertSettingViewModel(_languageService, _loggerFactory.CreateLogger<ConvertSettingViewModel>(), _presetService);

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

        // 설정창이 종료될 때 자동 저장 및 결과에 따른 알림 표시
        bool saved = await _presetService.SaveAsync();
        if (saved)
            _snackbarService.Show(GetString("Msg_Preset_SaveSuccess"), SnackbarType.Success);
        else
            _snackbarService.Show(GetString("Msg_Preset_SaveError"), SnackbarType.Error);
    }

    /// <summary>
    /// 목록에 있는 파일들에 대해 실제 변환 프로세스를 실행합니다.
    /// 비즈니스 로직에 따라 개별 파일의 상태와 진행률을 업데이트합니다.
    /// </summary>
    private async Task ConvertFilesAsync()
    {
        if (_fileList.Items.Count == 0) return;

        // 변환 전 확인창 띄우기
        var availableCount = _fileList.Items.Count - _fileList.UnsupportedCount;
        if (availableCount <= 0) return;

        bool isConfirmed = await _dialogService.ShowConfirmationAsync(
            string.Format(GetString("Dlg_Ask_Convert"), availableCount),
            GetString("Dlg_Title_Convert"));

        if (!isConfirmed) return;

        // 변환 전 설정 값 검증
        if (!_presetService.ValidPresetData(out string errorKey))
        {
            _snackbarService.Show(GetString(errorKey), SnackbarType.Error);
            return;
        }

        RequestStatus(AppStatus.Converting); // 애플리케이션을 변환 중 상태로 전환
        try
        {
            int processedCount = 0;
            int totalConvertCount = _fileList.Items.Count(item => item.Status != FileConvertStatus.Unsupported);
            int failCount = 0;
            string presetName = _presetService.Config.LastSelectedPresetName ?? string.Empty;

            foreach (var item in _fileList.Items)
            {
                // 미지원 파일은 건너뜀
                if (item.Status == FileConvertStatus.Unsupported) continue;

                item.Status = FileConvertStatus.Processing;
                item.Progress = 0;

                WeakReferenceMessenger.Default.Send(new ConvertProgressMessage
                {
                    FileName = System.IO.Path.GetFileName(item.Path),
                    ProcessedCount = processedCount,
                    TotalCount = totalConvertCount,
                    FailCount = failCount,
                    PresetName = presetName
                });

                // 시뮬레이션: 실제 변환 로직이 들어갈 자리
                for (int i = 0; i <= 100; i += 20)
                {
                    item.Progress = i;

                    // 전체 진행률 갱신
                    double totalProg = (processedCount + (i / 100.0)) / totalConvertCount * 100.0;
                    ConvertProgressPercent = (int)totalProg;

                    await Task.Delay(100);
                }

                item.Status = FileConvertStatus.Success;
                processedCount++;
                ConvertProgressPercent = (int)((double)processedCount / totalConvertCount * 100.0);
            }
        }
        finally
        {
            ConvertProgressPercent = 0;
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
