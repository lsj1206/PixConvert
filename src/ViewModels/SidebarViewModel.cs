using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using PixConvert.Models;
using PixConvert.Services;

namespace PixConvert.ViewModels;

/// <summary>
/// 좌측 사이드바의 액션(파일 추가, 변환, 설정 오픈 등)을 관리하는 뷰모델입니다.
/// </summary>
public partial class SidebarViewModel : ObservableObject
{
    private const int MaxItemCount = 10000;

    private readonly ILogger<SidebarViewModel> _logger;
    private readonly IDialogService _dialogService;
    private readonly ISnackbarService _snackbarService;
    private readonly ILanguageService _languageService;
    private readonly IFileProcessingService _fileProcessingService;
    private readonly ISortingService _sortingService;

    // 타 뷰모델 참조
    private readonly FileListViewModel _fileList;
    private readonly SettingsViewModel _settings;

    // 부모 상태 제어용 델리게이트
    private readonly Func<bool> _isBusyChecker;
    private readonly Action<bool> _isBusySetter;

    public IRelayCommand AddFilesCommand { get; }
    public IRelayCommand AddFolderCommand { get; }
    public IRelayCommand OpenConvertSettingCommand { get; }
    public IAsyncRelayCommand ConvertFilesCommand { get; }

    public SidebarViewModel(
        ILogger<SidebarViewModel> logger,
        IDialogService dialogService,
        ISnackbarService snackbarService,
        ILanguageService languageService,
        IFileProcessingService fileProcessingService,
        ISortingService sortingService,
        FileListViewModel fileList,
        SettingsViewModel settings,
        Func<bool> isBusyChecker,
        Action<bool> isBusySetter)
    {
        _logger = logger;
        _dialogService = dialogService;
        _snackbarService = snackbarService;
        _languageService = languageService;
        _fileProcessingService = fileProcessingService;
        _sortingService = sortingService;
        _fileList = fileList;
        _settings = settings;
        _isBusyChecker = isBusyChecker;
        _isBusySetter = isBusySetter;

        AddFilesCommand = new RelayCommand(AddFiles, () => !IsBusy);
        AddFolderCommand = new RelayCommand(AddFolder, () => !IsBusy);
        OpenConvertSettingCommand = new RelayCommand(OpenConvertSetting);
        ConvertFilesCommand = new AsyncRelayCommand(ConvertFilesAsync, () => !IsBusy);

        // 정렬 옵션 변경 시 리스트 정렬 유지
        _settings.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(SettingsViewModel.SelectedSortOption) ||
                e.PropertyName == nameof(SettingsViewModel.IsSortAscending))
            {
                SortFiles();
            }
            else if (e.PropertyName == nameof(SettingsViewModel.ShowMismatchOnly))
            {
                ApplyFilter();
            }
        };
    }

    private bool IsBusy => _isBusyChecker();

    public void NotifyCommandsStateChanged()
    {
        AddFilesCommand.NotifyCanExecuteChanged();
        AddFolderCommand.NotifyCanExecuteChanged();
        ConvertFilesCommand.NotifyCanExecuteChanged();
    }

    private async void AddFiles()
    {
        var dialog = new OpenFileDialog { Multiselect = true, Title = GetString("Dlg_Title_AddFile") };
        if (dialog.ShowDialog() == true) await ProcessFiles(dialog.FileNames);
    }

    private async void AddFolder()
    {
        var dialog = new OpenFolderDialog { Multiselect = true, Title = GetString("Dlg_Title_AddFolder") };
        if (dialog.ShowDialog() == true) await ProcessFiles(dialog.FolderNames);
    }

    public async Task ProcessFiles(IEnumerable<string> paths)
    {
        _isBusySetter(true);
        try
        {
            var pathList = paths.ToList();
            _logger.LogInformation(GetString("Log_Main_ProcessStart"), pathList.Count);
            _snackbarService.ShowProgress(GetString("Msg_LoadingFile"));

            var progress = new Progress<FileProcessingProgress>(p =>
            {
                _snackbarService.UpdateProgress(string.Format(GetString("Msg_LoadingFileProgress"), p.CurrentIndex, p.TotalCount));
            });

            var result = await _fileProcessingService.ProcessPathsAsync(
                paths,
                MaxItemCount,
                _fileList.Items.Count,
                progress);

            if (result.SuccessCount == 0 && result.IgnoredCount > 0)
            {
                var msg = string.Format(GetString("Msg_MaxItemExceeded"), MaxItemCount, _fileList.Items.Count, result.IgnoredCount);
                _snackbarService.Show(msg, SnackbarType.Error);
                return;
            }

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
            _isBusySetter(false);
        }
    }

    private void AddFilesToList(List<FileItem> items)
    {
        int totalCount = items.Count;
        int successCount = _fileList.AddRange(items);
        SortFiles();

        if (successCount == 0 && totalCount > 0)
            _snackbarService.Show(GetString("Msg_AlreadyExists"), SnackbarType.Error);
        else if (successCount < totalCount)
            _snackbarService.Show(string.Format(GetString("Msg_AddFilePartial"), totalCount, successCount), SnackbarType.Warning);
        else
            _snackbarService.Show(string.Format(GetString("Msg_AddFile"), successCount), SnackbarType.Success);
    }

    private void OpenConvertSetting()
    {
        // TODO: ContentDialog 구현 예정
    }

    private async Task ConvertFilesAsync()
    {
        if (_fileList.Items.Count == 0) return;

        _isBusySetter(true);
        try
        {
            foreach (var item in _fileList.Items)
            {
                if (item.Status == FileConvertStatus.Unsupported) continue;

                item.Status = FileConvertStatus.Processing;
                item.Progress = 0;

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
            _isBusySetter(false);
        }
    }

    private void SortFiles()
    {
        if (_settings.SelectedSortOption == null) return;
        _fileList.Sorting(_sortingService, _settings.SelectedSortOption, _settings.IsSortAscending);
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var view = System.Windows.Data.CollectionViewSource.GetDefaultView(_fileList.Items);
        if (_settings.ShowMismatchOnly)
        {
            view.Filter = item => item is FileItem fileItem && fileItem.IsMismatch;
        }
        else
        {
            view.Filter = null;
        }
        view.Refresh();
    }

    private string GetString(string key) => _languageService.GetString(key);
}
