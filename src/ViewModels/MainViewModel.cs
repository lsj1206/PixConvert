using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Globalization;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.Win32;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using PixConvert.Models;
using PixConvert.Services;

namespace PixConvert.ViewModels;

/// <summary>
/// 애플리케이션의 메인 화면 로직과 데이터 처리를 담당하는 메인 뷰모델 클래스입니다.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private const int MaxItemCount = 10000; // 최대 허용 파일 개수

    /// <summary>하단 알림(스낵바) 제어를 위한 뷰모델</summary>
    public SnackbarViewModel Snackbar { get; }

    /// <summary>파일 목록 데이터 및 조작을 담당하는 뷰모델</summary>
    public FileListViewModel FileList { get; } = new();

    /// <summary>환경 설정 및 공유 상태 관리를 위한 뷰모델</summary>
    public SettingsViewModel Settings { get; }

    [ObservableProperty] private bool isBusy = false;

    /// <summary>목록의 전체 파일 수</summary>
    public int TotalCount => FileList.Items.Count;

    /// <summary>미지원(시그니처 미판별) 파일 수</summary>
    public int UnsupportedCount => FileList.Items.Count(x => x.FileSignature == "-");

    // 명령 정의
    public IRelayCommand AddFilesCommand { get; }
    public IRelayCommand AddFolderCommand { get; }
    public IRelayCommand DeleteFilesCommand { get; }
    public IRelayCommand ListClearCommand { get; }
    public IRelayCommand ReorderNumberCommand { get; }
    public IRelayCommand<SortType> SortByColumnCommand { get; }

    private readonly IDialogService _dialogService;
    private readonly ISnackbarService _snackbarService;
    private readonly ISortingService _sortingService;
    private readonly ILanguageService _languageService;
    private readonly IFileProcessingService _fileProcessingService;
    private readonly ILogger<MainViewModel> _logger;

    /// <summary>
    /// MainViewModel의 새 인스턴스를 초기화하며 필요한 서비스를 주입받습니다.
    /// </summary>
    public MainViewModel(
        ILogger<MainViewModel> logger,
        IDialogService dialogService,
        ISnackbarService snackbarService,
        ISortingService sortingService,
        ILanguageService languageService,
        IFileProcessingService fileProcessingService,
        SnackbarViewModel snackbarViewModel,
        SettingsViewModel settingsViewModel)
    {
        _logger = logger;
        _dialogService = dialogService;
        _snackbarService = snackbarService;
        _sortingService = sortingService;
        _languageService = languageService;
        _fileProcessingService = fileProcessingService;
        Snackbar = snackbarViewModel;
        Settings = settingsViewModel;

        // 명령 초기화 및 메서드 연결 (IsBusy가 아닐 때만 실행 가능)
        AddFilesCommand = new RelayCommand(AddFiles, () => !IsBusy);
        AddFolderCommand = new RelayCommand(AddFolder, () => !IsBusy);
        DeleteFilesCommand = new RelayCommand<System.Collections.IList>(DeleteFiles, _ => !IsBusy);
        ListClearCommand = new AsyncRelayCommand(ListClearAsync, () => !IsBusy);
        ReorderNumberCommand = new RelayCommand(ReorderNumber, () => !IsBusy && !Settings.ShowMismatchOnly);
        SortByColumnCommand = new RelayCommand<SortType>(SortByColumn);

        // 설정 변경 이벤트 구독
        Settings.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(SettingsViewModel.SelectedSortOption) ||
                e.PropertyName == nameof(SettingsViewModel.IsSortAscending))
            {
                SortFiles();
            }
            else if (e.PropertyName == nameof(SettingsViewModel.ShowMismatchOnly))
            {
                ApplyFilter();
                ReorderNumberCommand.NotifyCanExecuteChanged();
            }
        };

        // 파일 목록 변경 감지하여 통계 정보 갱신
        ((System.Collections.Specialized.INotifyCollectionChanged)FileList.Items).CollectionChanged += (s, e) =>
        {
            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(UnsupportedCount));
        };
    }

    /// <summary>파일 추가 다이얼로그를 통해 리스트에 파일을 추가합니다.</summary>
    private async void AddFiles()
    {
        var dialog = new OpenFileDialog { Multiselect = true, Title = GetString("Dlg_Title_AddFile") };
        if (dialog.ShowDialog() == true) await ProcessFiles(dialog.FileNames);
    }

    /// <summary>폴더 선택 다이얼로그를 통해 폴더 내부의 모든 파일을 리스트에 추가합니다.</summary>
    private async void AddFolder()
    {
        var dialog = new OpenFolderDialog { Multiselect = true, Title = GetString("Dlg_Title_AddFolder") };
        if (dialog.ShowDialog() == true) await ProcessFiles(dialog.FolderNames);
    }

    /// <summary>외부에서 드롭된 파일/폴더 경로 목록을 처리합니다.</summary>
    public async void DropFiles(string[] paths)
    {
        if (IsBusy) return;
        await ProcessFiles(paths);
    }

    /// <summary>
    /// 입력받은 경로들을 분석하여 파일을 추출하고, 중복 및 개수 확인 후 목록에 추가합니다.
    /// </summary>
    private async Task ProcessFiles(IEnumerable<string> paths)
    {
        IsBusy = true;
        try
        {
            var pathList = paths.ToList();
            _logger.LogInformation(GetString("Log_Main_ProcessStart"), pathList.Count);

            _snackbarService.ShowProgress(GetString("Msg_LoadingFile"));

            // 진행률 보고를 위한 IProgress 정의
            var progress = new Progress<FileProcessingProgress>(p =>
            {
                _snackbarService.UpdateProgress(string.Format(GetString("Msg_LoadingFileProgress"), p.CurrentIndex, p.TotalCount));
            });

            // 서비스 호출하여 파일 처리 수행
            var result = await _fileProcessingService.ProcessPathsAsync(
                paths,
                MaxItemCount,
                FileList.Items.Count,
                progress);

            // 정책 위반(최대 수량 초과) 처리
            if (result.SuccessCount == 0 && result.IgnoredCount > 0)
            {
                _logger.LogWarning(GetString("Log_Main_MaxExceeded"), result.IgnoredCount);

                var msg = string.Format(GetString("Msg_MaxItemExceeded"), MaxItemCount, FileList.Items.Count, result.IgnoredCount);
                _snackbarService.Show(msg, Services.SnackbarType.Error);
                return;
            }

            _logger.LogInformation(GetString("Log_Main_ProcessComplete"), result.SuccessCount, result.IgnoredCount);

            if (result.SuccessCount > 0)
            {
                AddFilesToList(result.NewItems);
            }
        }
        catch (Exception ex)
        {
            _snackbarService.Show(string.Format(GetString("Msg_Error_Occurred"), ex.Message), Services.SnackbarType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>IsBusy 상태가 변경될 때 명령의 실행 가능 여부를 다시 확인합니다.</summary>
    partial void OnIsBusyChanged(bool value)
    {
        AddFilesCommand.NotifyCanExecuteChanged();
        AddFolderCommand.NotifyCanExecuteChanged();
        DeleteFilesCommand.NotifyCanExecuteChanged();
        ListClearCommand.NotifyCanExecuteChanged();
        ReorderNumberCommand.NotifyCanExecuteChanged();
    }

    /// <summary>최종 생성된 아이템들을 실제 목록 컬렉션에 삽입합니다.</summary>
    private void AddFilesToList(List<FileItem> items)
    {
        int totalCount = items.Count;
        if (totalCount == 0) return;

        int successCount = FileList.AddRange(items);
        SortFiles();

        // 결과 메시지 출력
        if (successCount == 0 && totalCount > 0)
            _snackbarService.Show(GetString("Msg_AlreadyExists"), Services.SnackbarType.Error);
        else if (successCount < totalCount)
            _snackbarService.Show(string.Format(GetString("Msg_AddFilePartial"), totalCount, successCount), SnackbarType.Warning);
        else
            _snackbarService.Show(string.Format(GetString("Msg_AddFile"), successCount), SnackbarType.Success);
    }

    /// <summary>현재 목록의 순서에 맞춰 추가 인덱스(순번)를 다시 부여합니다.</summary>
    private async void ReorderNumber()
    {
        if (FileList.Items.Count == 0) return;

        var result = await _dialogService.ShowConfirmationAsync(
                GetString("Dlg_Ask_ReorderIndex"), GetString("Dlg_Title_ReorderIndex"));

        if (result)
        {
            FileList.ReorderIndex();
            _snackbarService.Show(GetString("Msg_ReorderIndex"), SnackbarType.Success);
        }
    }

    /// <summary>선택된 파일들을 목록에서 제거합니다.</summary>
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

        _logger.LogInformation(GetString("Log_Main_DeleteRequest"), count);

        FileList.RemoveItems(itemsToDelete);
        _snackbarService.Show(string.Format(GetString("Msg_RemoveFile"), count), SnackbarType.Warning);
    }

    /// <summary>파일 목록을 완전히 초기화합니다.</summary>
    private async Task ListClearAsync()
    {
        if (FileList.Items.Count == 0) return;
        if (await _dialogService.ShowConfirmationAsync(GetString("Dlg_Ask_ClearList"), GetString("Dlg_Title_ClearList")))
        {
            _logger.LogInformation(GetString("Log_Main_ClearList"));
            FileList.Clear();
            _snackbarService.Show(GetString("Msg_ClearList"), SnackbarType.Success);
        }
    }

    /// <summary>특정 컬럼 헤더 클릭 시 호출되어 정렬을 수행합니다.</summary>
    private void SortByColumn(SortType type)
    {
        if (Settings.SelectedSortOption?.Type == type)
        {
            // 동일 컬럼 클릭 시 오름차순/내림차순 토글
            Settings.IsSortAscending = !Settings.IsSortAscending;
        }
        else
        {
            // 다른 컬럼 클릭 시 해당 타입으로 정렬 및 오름차순 초기화
            var option = Settings.SortOptions.FirstOrDefault(x => x.Type == type);
            if (option != null)
            {
                Settings.SelectedSortOption = option;
                Settings.IsSortAscending = true;
            }
        }
    }

    /// <summary>설정된 정렬 옵션에 따라 목록을 다시 정렬합니다.</summary>
    private void SortFiles()
    {
        if (Settings.SelectedSortOption == null) return;
        FileList.Sorting(_sortingService, Settings.SelectedSortOption, Settings.IsSortAscending);

        // 정렬 후 필터 상태 유지를 위해 필터 다시 적용
        ApplyFilter();
    }

    /// <summary>불일치 필터 활성화 여부에 따라 목록 노출 항목을 필터링합니다.</summary>
    private void ApplyFilter()
    {
        var view = System.Windows.Data.CollectionViewSource.GetDefaultView(FileList.Items);
        if (Settings.ShowMismatchOnly)
        {
            view.Filter = item => item is FileItem fileItem && fileItem.IsMismatch;
        }
        else
        {
            view.Filter = null;
        }
        view.Refresh();
    }

    private string GetString(string key)
    {
        return _languageService.GetString(key);
    }
}
