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
using PixConvert.Services.Interfaces;

namespace PixConvert.ViewModels;

/// <summary>
/// 파일 및 폴더 입력, 드래그 앤 드롭, 파일 분석(스캔) 책임을 전담하는 뷰모델입니다.
/// </summary>
public partial class FileInputViewModel : ViewModelBase
{
    private readonly ISnackbarService _snackbarService;
    private readonly IFileAnalyzerService _fileAnalyzerService;
    private readonly FileListViewModel _fileList;
    private readonly SortFilterViewModel _sortFilter;

    /// <summary>파일들을 개별적으로 선택하여 목록에 추가하는 명령</summary>
    public IAsyncRelayCommand AddFilesCommand { get; }
    /// <summary>폴더를 선택하여 내부의 파일들을 목록에 추가하는 명령</summary>
    public IAsyncRelayCommand AddFolderCommand { get; }

    /// <summary>
    /// FileInputViewModel의 새 인스턴스를 초기화합니다.
    /// </summary>
    public FileInputViewModel(
        ILogger<FileInputViewModel> logger,
        ILanguageService languageService,
        ISnackbarService snackbarService,
        IFileAnalyzerService fileAnalyzerService,
        FileListViewModel fileList,
        SortFilterViewModel sortFilter)
        : base(languageService, logger)
    {
        _snackbarService = snackbarService;
        _fileAnalyzerService = fileAnalyzerService;
        _fileList = fileList;
        _sortFilter = sortFilter;

        // 명령 초기화: 작업 중이 아닐 때만 실행 가능
        AddFilesCommand = new AsyncRelayCommand(AddFilesAsync, () => CurrentStatus != AppStatus.Converting);
        AddFolderCommand = new AsyncRelayCommand(AddFolderAsync, () => CurrentStatus != AppStatus.Converting);
    }

    /// <summary>상태 변경 시 파일 추가 명령들의 실행 가능 여부를 갱신합니다.</summary>
    protected override void OnStatusChanged(AppStatus newStatus)
    {
        AddFilesCommand.NotifyCanExecuteChanged();
        AddFolderCommand.NotifyCanExecuteChanged();
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
    /// 외부(MainViewModel 등)에서 드롭 이벤트를 수신하여 파일들을 처리합니다.
    /// </summary>
    public async Task DropFilesAsync(string[] paths)
    {
        if (paths == null || paths.Length == 0 || CurrentStatus == AppStatus.Converting)
            return;

        await ProcessFiles(paths);
    }

    /// <summary>
    /// 실제 경로(파일 또는 폴더)들을 바탕으로 파일 정보를 추출하고 목록에 추가하는 비즈니스 로직입니다.
    /// </summary>
    public async Task ProcessFiles(IEnumerable<string> paths)
    {
        if (CurrentStatus == AppStatus.Converting) return; // 변환 중에는 무시

        RequestStatus(AppStatus.FileAdd);
        try
        {
            var pathList = paths.ToList();
            _logger.LogInformation(GetString("Log_Sidebar_ProcessStart"), pathList.Count);
            _snackbarService.ShowProgress(GetString("Msg_LoadingFile"));

            // 진행률 보고 대리자 구성
            var progress = new Progress<FileProcessingProgress>(p =>
                _snackbarService.UpdateProgress(string.Format(GetString("Msg_LoadingFileProgress"), p.CurrentIndex, p.TotalCount)));

            // 1. 서비스 엔진을 통해 파일 스캔 및 객체 생성
            var result = await _fileAnalyzerService.ProcessPathsAsync(paths, 10000, _fileList.Items.Count, _fileList.PathSet, progress);

            // 2. 추가 가능한 파일이 없는 경우 결과 처리
            if (result.SuccessCount == 0 && (result.IgnoredCount > 0 || result.DuplicateCount > 0))
            {
                if (result.IgnoredCount > 0)
                    _snackbarService.Show(GetString("Msg_LimitReached"), SnackbarType.Error);
                else
                    _snackbarService.Show(GetString("Msg_NoNewFiles"), SnackbarType.Error);
                return;
            }

            // 3. 스캔 성공 시 목록에 삽입 및 메시지 출력
            if (result.SuccessCount > 0)
            {
                int addedCount = AddFilesToList(result.NewItems);
                if (addedCount == 0)
                {
                    _snackbarService.Show(GetString("Msg_NoNewFiles"), SnackbarType.Error);
                    return;
                }

                if (result.IgnoredCount > 0)
                    _snackbarService.Show(string.Format(GetString("Msg_AddWithLimit"), addedCount, result.IgnoredCount), SnackbarType.Warning);
                else if (result.DuplicateCount > 0)
                    _snackbarService.Show(string.Format(GetString("Msg_AddWithDuplicate"), addedCount, result.DuplicateCount), SnackbarType.Warning);
                else
                    _snackbarService.Show(string.Format(GetString("Msg_AddFile"), addedCount), SnackbarType.Success);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, GetString("Log_Sidebar_ProcessError"));
            _snackbarService.Show(string.Format(GetString("Msg_Error_Occurred"), ex.Message), SnackbarType.Error);
        }
        finally
        {
            RequestStatus(AppStatus.Idle);
        }
    }

    /// <summary>
    /// FileListViewModel 데이터 소스에 항목을 추가하고, 정렬을 재적용합니다.
    /// </summary>
    private int AddFilesToList(List<FileItem> items)
    {
        int successCount = _fileList.AddRange(items);
        _sortFilter.ApplySortAndFilter();
        return successCount;
    }
}
