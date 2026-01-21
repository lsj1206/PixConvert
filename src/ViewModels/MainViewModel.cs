using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Win32;
using PixConvert.Services;
using PixConvert.Models;

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

    /// <summary>정렬 기준 유형 정의</summary>
    public enum SortType
    {
        /// <summary>목록 추가 순서</summary>
        AddIndex,
        /// <summary>파일 이름 (숫자 인식 정렬)</summary>
        NameNumber,
        /// <summary>파일 이름 및 경로</summary>
        NamePath,
        /// <summary>경로 및 번호</summary>
        PathNumber,
        /// <summary>경로 및 파일 이름</summary>
        PathName,
        /// <summary>파일 크기</summary>
        Size,
        /// <summary>파일 생성 날짜</summary>
        CreatedDate,
        /// <summary>파일 수정 날짜</summary>
        ModifiedDate
    }

    /// <summary>정렬 옵션 정보를 담는 클래스</summary>
    public class SortOption
    {
        public string Display { get; set; } = string.Empty;
        public SortType Type { get; set; }
    }

    /// <summary>사용 가능한 정렬 옵션 목록</summary>
    public ObservableCollection<SortOption> SortOptions { get; } =
    [
        new() { Display = "번호", Type = SortType.AddIndex },
        new() { Display = "이름-번호", Type = SortType.NameNumber },
        new() { Display = "이름-경로", Type = SortType.NamePath },
        new() { Display = "경로-번호", Type = SortType.PathNumber },
        new() { Display = "경로-이름", Type = SortType.PathName },
        new() { Display = "크기", Type = SortType.Size },
        new() { Display = "생성일", Type = SortType.CreatedDate },
        new() { Display = "수정일", Type = SortType.ModifiedDate }
    ];

    [ObservableProperty] private bool isBusy = false;

    [ObservableProperty] private SortOption selectedSortOption;
    [ObservableProperty] private bool isSortAscending = true;
    [ObservableProperty] private bool confirmDeletion = true;
    [ObservableProperty] private bool showExtension = true;

    // 명령 정의
    public IRelayCommand AddFilesCommand { get; }
    public IRelayCommand AddFolderCommand { get; }
    public IRelayCommand DeleteFilesCommand { get; }
    public IRelayCommand ListClearCommand { get; }
    public IRelayCommand ReorderNumberCommand { get; }

    private readonly IDialogService _dialogService;
    private readonly ISnackbarService _snackbarService;
    private readonly IFileService _fileService;
    private readonly ISortingService _sortingService;

    /// <summary>
    /// MainViewModel의 새 인스턴스를 초기화하며 필요한 서비스를 주입받습니다.
    /// </summary>
    public MainViewModel(
        IDialogService dialogService,
        ISnackbarService snackbarService,
        IFileService fileService,
        ISortingService sortingService,
        SnackbarViewModel snackbarViewModel)
    {
        _dialogService = dialogService;
        _snackbarService = snackbarService;
        _fileService = fileService;
        _sortingService = sortingService;
        Snackbar = snackbarViewModel;

        // 명령 초기화 및 메서드 연결
        AddFilesCommand = new RelayCommand(AddFiles);
        AddFolderCommand = new RelayCommand(AddFolder);
        DeleteFilesCommand = new RelayCommand<System.Collections.IList>(DeleteFiles);
        ListClearCommand = new AsyncRelayCommand(ListClearAsync);
        ReorderNumberCommand = new RelayCommand(ReorderNumber);

        // 초기 정렬 상태 설정
        SelectedSortOption = SortOptions[0];
    }

    /// <summary>파일 추가 다이얼로그를 통해 리스트에 파일을 추가합니다.</summary>
    private async void AddFiles()
    {
        var dialog = new OpenFileDialog { Multiselect = true, Title = "파일 추가" };
        if (dialog.ShowDialog() == true) await ProcessFiles(dialog.FileNames);
    }

    /// <summary>폴더 선택 다이얼로그를 통해 폴더 내부의 모든 파일을 리스트에 추가합니다.</summary>
    private async void AddFolder()
    {
        var dialog = new OpenFolderDialog { Multiselect = true, Title = "폴더 추가" };
        if (dialog.ShowDialog() == true) await ProcessFiles(dialog.FolderNames);
    }

    /// <summary>외부에서 드롭된 파일/폴더 경로 목록을 처리합니다.</summary>
    public async void DropFiles(string[] paths) => await ProcessFiles(paths);

    /// <summary>
    /// 입력받은 경로들을 분석하여 파일을 추출하고, 중복 및 개수 확인 후 목록에 추가합니다.
    /// </summary>
    private async Task ProcessFiles(IEnumerable<string> paths)
    {
        IsBusy = true;
        try
        {
            var rawPaths = paths.ToList();
            var files = rawPaths.Where(File.Exists).ToList();
            var folders = rawPaths.Where(Directory.Exists).ToList();
            var finalPaths = new List<string>(files);

            // 폴더 내 파일 검색 작업
            if (folders.Count > 0)
            {
                await Task.Run(() =>
                {
                    foreach (var folderPath in folders)
                        finalPaths.AddRange(_fileService.GetFilesInFolder(folderPath));
                });
            }

            int currentCount = FileList.Items.Count;
            int addCount = finalPaths.Count;

            // 정책 검사: 최대 수량 초과 여부
            if (currentCount + addCount > MaxItemCount)
            {
                _snackbarService.Show($"총 파일 개수가 {MaxItemCount:N0}개를 초과하여 작업을 취소합니다.\n(현재: {currentCount:N0} + 추가: {addCount:N0})", Services.SnackbarType.Error);
                return;
            }

            if (addCount == 0) return;

            _snackbarService.ShowProgress("파일 정보를 읽는 중...");

            // FileItem 객체 생성 (병렬화 가능성이 있는 연산이므로 별도 스레드에서 진행)
            var newItems = await Task.Run(() =>
            {
                var items = new List<FileItem>(addCount);
                for (int i = 0; i < addCount; i++)
                {
                    var item = _fileService.CreateFileItem(finalPaths[i]);
                    if (item != null)
                    {
                        item.UpdateDisplay(ShowExtension);
                        items.Add(item);
                    }

                    // 진행률 업데이트 (100개 단위)
                    if (i % 100 == 0 || i == addCount - 1)
                    {
                        double percent = (double)(i + 1) / addCount * 100;
                        _snackbarService.UpdateProgress($"파일 읽는 중... ({percent:0}%)");
                    }
                }
                return items;
            });

            AddFilesToList(newItems);
        }
        catch (Exception ex)
        {
            _snackbarService.Show($"작업 중 오류 발생: {ex.Message}", Services.SnackbarType.Error);
        }
        finally
        {
            IsBusy = false;
        }
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
            _snackbarService.Show("이미 목록에 있는 파일들입니다.", Services.SnackbarType.Error);
        else if (successCount < totalCount)
            _snackbarService.Show($"{totalCount}개 중 {successCount}개를 새로 추가했습니다.", SnackbarType.Warning);
        else
            _snackbarService.Show($"{successCount}개의 파일을 추가했습니다.", SnackbarType.Success);
    }

    /// <summary>현재 목록의 순서에 맞춰 추가 인덱스(순번)를 다시 부여합니다.</summary>
    private async void ReorderNumber()
    {
        if (FileList.Items.Count == 0) return;

        var result = await _dialogService.ShowConfirmationAsync(
                "현재 표시된 순서대로 번호를 다시 부여하시겠습니까?", "번호 재정렬");

        if (result)
        {
            FileList.ReorderIndex();
            _snackbarService.Show("번호를 다시 설정했습니다.", SnackbarType.Success);
        }
    }

    /// <summary>확장자 표시 여부가 변경될 때 모든 아이템의 표시 이름을 갱신합니다.</summary>
    partial void OnShowExtensionChanged(bool value)
    {
        foreach (var item in FileList.Items) item.UpdateDisplay(value);
    }

    /// <summary>선택된 파일들을 목록에서 제거합니다.</summary>
    private async void DeleteFiles(System.Collections.IList? items)
    {
        if (items == null || items.Count == 0) return;

        var itemsToDelete = items.Cast<FileItem>().ToList();
        int count = itemsToDelete.Count;

        if (ConfirmDeletion)
        {
            string message = count == 1 ? "이 파일을 제거하시겠습니까?" : $"{count}개의 파일을 제거하시겠습니까?";
            if (!await _dialogService.ShowConfirmationAsync(message, "삭제 확인")) return;
        }

        FileList.RemoveItems(itemsToDelete);
        _snackbarService.Show($"{count}개를 제거했습니다.", SnackbarType.Warning);
    }

    /// <summary>파일 목록을 완전히 초기화합니다.</summary>
    private async Task ListClearAsync()
    {
        if (FileList.Items.Count == 0) return;
        if (await _dialogService.ShowConfirmationAsync("목록의 모든 파일을 제거하시겠습니까?", "전체 삭제"))
        {
            FileList.Clear();
            _snackbarService.Show("목록을 비웠습니다.", SnackbarType.Success);
        }
    }

    /// <summary>설정된 정렬 옵션에 따라 목록을 다시 정렬합니다.</summary>
    private void SortFiles()
    {
        if (SelectedSortOption == null) return;
        FileList.Sorting(_sortingService, SelectedSortOption, IsSortAscending);
    }

    partial void OnSelectedSortOptionChanged(SortOption value) => SortFiles();
    partial void OnIsSortAscendingChanged(bool value) => SortFiles();
}
