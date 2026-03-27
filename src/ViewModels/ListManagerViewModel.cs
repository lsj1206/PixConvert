using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using PixConvert.Models;
using PixConvert.Services;
using PixConvert.Services.Interfaces;

namespace PixConvert.ViewModels;

/// <summary>
/// 파일 목록 정리, 미지원 확장자 관리 등 리스트 관리 모드를 전담하는 뷰모델입니다.
/// </summary>
public partial class ListManagerViewModel : ViewModelBase
{
    private readonly ISnackbarService _snackbarService;
    private readonly IDialogService _dialogService;
    private readonly FileListViewModel _fileList;

    /// <summary>목록 설정 모드에서 선택가능한 포맷 태그 목록</summary>
    public ObservableCollection<FormatTagViewModel> FormatTags { get; } = new();

    [ObservableProperty] private bool _removeUnsupported;
    [ObservableProperty] private bool _confirmDeletion = true;

    public IRelayCommand EnterListManagerCommand { get; }
    public IRelayCommand ExitListManagerCommand { get; }
    public IRelayCommand ClearSelectedCommand { get; }
    public IAsyncRelayCommand RemoveAllListCommand { get; }
    public IAsyncRelayCommand<System.Collections.IList> DeleteFilesCommand { get; }

    public ListManagerViewModel(
        ILogger<ListManagerViewModel> logger,
        ILanguageService languageService,
        ISnackbarService snackbarService,
        IDialogService dialogService,
        FileListViewModel fileList)
        : base(languageService, logger)
    {
        _snackbarService = snackbarService;
        _dialogService = dialogService;
        _fileList = fileList;

        EnterListManagerCommand = new RelayCommand(EnterListManager, () => CurrentStatus == AppStatus.Idle);
        ExitListManagerCommand = new RelayCommand(ExitListManager, () => CurrentStatus == AppStatus.ListManager);
        ClearSelectedCommand = new RelayCommand(ClearSelected, () => CurrentStatus == AppStatus.ListManager);
        RemoveAllListCommand = new AsyncRelayCommand(RemoveAllListAsync, () => CurrentStatus == AppStatus.ListManager);
        DeleteFilesCommand = new AsyncRelayCommand<System.Collections.IList>(DeleteFilesAsync, _ => CurrentStatus == AppStatus.Idle || CurrentStatus == AppStatus.ListManager);
    }

    protected override void OnStatusChanged(AppStatus newStatus)
    {
        EnterListManagerCommand.NotifyCanExecuteChanged();
        ExitListManagerCommand.NotifyCanExecuteChanged();
        ClearSelectedCommand.NotifyCanExecuteChanged();
        RemoveAllListCommand.NotifyCanExecuteChanged();
        DeleteFilesCommand.NotifyCanExecuteChanged();

        // 뷰모델 상태 불일치 방지
        if (newStatus == AppStatus.Converting && CurrentStatus == AppStatus.ListManager)
        {
            ExitListManager();
        }
    }

    private void EnterListManager()
    {
        FormatTags.Clear();
        RemoveUnsupported = false;

        var signatures = _fileList.Items
            .Where(i => i.Status != FileConvertStatus.Unsupported && i.FileSignature != "-")
            .Select(i => i.FileSignature)
            .Distinct();

        foreach (var sig in signatures)
            FormatTags.Add(new FormatTagViewModel(sig));

        RequestStatus(AppStatus.ListManager);
    }

    private void ExitListManager() => RequestStatus(AppStatus.Idle);

    private void ClearSelected()
    {
        var extsToRemove = FormatTags
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
            _snackbarService.Show(string.Format(GetString("Msg_RemoveFile"), itemsToRemove.Count), SnackbarType.Warning);
        }

        EnterListManager(); // 목록 갱신 후 태그 재초기화
    }

    private async Task RemoveAllListAsync()
    {
        if (_fileList.Items.Count == 0) return;

        bool confirmed = await _dialogService.ShowConfirmationAsync(
            GetString("Dlg_Ask_ClearList"),
            GetString("Dlg_Title_ClearList"));

        if (confirmed)
        {
            _fileList.Clear();
            FormatTags.Clear();
            RemoveUnsupported = false;
            _snackbarService.Show(GetString("Msg_ClearList"), SnackbarType.Warning);
        }
    }

    private async Task DeleteFilesAsync(System.Collections.IList? items)
    {
        if (items == null || items.Count == 0) return;

        var prevStatus = CurrentStatus;
        RequestStatus(AppStatus.Processing);
        try
        {
            var itemsToDelete = items.Cast<FileItem>().ToList();
            int count = itemsToDelete.Count;

            if (ConfirmDeletion)
            {
                string message = count == 1 ? GetString("Dlg_Ask_DeleteSingle") : string.Format(GetString("Dlg_Ask_DeleteMulti"), count);
                if (!await _dialogService.ShowConfirmationAsync(message, GetString("Dlg_Title_DeleteConfirm"))) return;
            }

            _fileList.RemoveItems(itemsToDelete);
            _snackbarService.Show(string.Format(GetString("Msg_RemoveFile"), count), SnackbarType.Warning);

            if (prevStatus == AppStatus.ListManager)
            {
                EnterListManager(); // 태그 재구성
            }
        }
        finally
        {
            RequestStatus(prevStatus);
        }
    }
}
