using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Mvvm.Input;
using PixConvert.Models;
using PixConvert.Services;

namespace PixConvert.ViewModels;

/// <summary>
/// 파일 목록 데이터의 관리와 조작(추가, 삭제, 정렬, 이동 등)을 담당하는 뷰모델입니다.
/// </summary>
public class FileListViewModel : ViewModelBase
{
    /// <summary>파일 항목들을 저장하는 실제 데이터 컬렉션 (단위 테스트 접근을 위해 internal)</summary>
    internal readonly ObservableCollection<FileItem> _items = new();

    /// <summary>화면에 바인딩되는 읽기 전용 파일 아이템 컬렉션</summary>
    public ReadOnlyObservableCollection<FileItem> Items { get; }

    /// <summary>중복 체크용 HashSet (O(1) 탐색, 대소문자 무시)</summary>
    private readonly HashSet<string> _pathSet = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>중복 체크용 경로 집합 (읽기 전용 노출)</summary>
    public IReadOnlySet<string> PathSet => _pathSet;

    /// <summary>정렬 수행 중 불필요한 이벤트 및 통계 재계산을 방지하기 위한 가드 플래그</summary>
    private bool _isSorting;

    /// <summary>목록의 전체 파일 수</summary>
    public int TotalCount => Items.Count;

    /// <summary>미지원(시그니처 미판별) 파일 수</summary>
    public int UnsupportedCount => Items.Count(x => x.IsUnsupported);

    /// <summary>다음에 추가될 아이템의 기본 순번</summary>
    private int _nextAddIndex = 1;

    public IRelayCommand<MoveItemsRequest> MoveItemsCommand { get; }

    /// <summary>
    /// FileListViewModel의 새 인스턴스를 초기화합니다.
    /// </summary>
    public FileListViewModel(ILanguageService languageService, ILogger<FileListViewModel> logger)
    : base(languageService, logger)
    {
        Items = new ReadOnlyObservableCollection<FileItem>(_items);
        MoveItemsCommand = new RelayCommand<MoveItemsRequest>(MoveItems);

        // 컬렉션 변경 시 통계 갱신 및 UI 알림
        _items.CollectionChanged += (s, e) =>
        {
            if (_isSorting) return;
            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(UnsupportedCount));
        };

    }

    /// <summary>
    /// 목록의 모든 데이터를 초기화합니다.
    /// </summary>
    public void Clear()
    {
        _items.Clear();
        _pathSet.Clear();
        _nextAddIndex = 1;
    }

    /// <summary>
    /// 단일 파일 아이템을 목록에 추가합니다. 중복된 경로는 추가되지 않습니다.
    /// </summary>
    /// <param name="item">추가할 파일 아이템</param>
    /// <returns>추가 성공 시 true, 중복 등으로 실패 시 false를 반환합니다.</returns>
    public bool AddItem(FileItem item)
    {
        // HashSet 기반 O(1) 중복 체크 (Add가 false를 반환하면 이미 존재하는 경로)
        if (!_pathSet.Add(item.Path))
            return false;

        item.AddIndex = _nextAddIndex++;
        _items.Add(item);
        return true;
    }

    /// <summary>
    /// 다수의 파일 아이템을 한꺼번에 추가합니다.
    /// </summary>
    /// <param name="newItems">추가할 아이템 목록</param>
    /// <returns>실제로 추가된 아이템의 개수를 반환합니다.</returns>
    public int AddRange(IEnumerable<FileItem> newItems)
    {
        int addedCount = 0;
        foreach (var item in newItems)
        {
            if (AddItem(item)) addedCount++;
        }
        return addedCount;
    }

    /// <summary>
    /// 주어진 정렬 서비스와 옵션에 따라 목록의 순서를 다시 배치합니다.
    /// </summary>
    /// <param name="sortingService">정렬 엔진 서비스</param>
    /// <param name="sortType">정렬 기준</param>
    /// <param name="ascending">오름차순 여부</param>
    public void Sorting(ISortingService sortingService, SortType sortType, bool ascending)
    {
        if (_items.Count == 0) return;

        var sortedItems = sortingService.Sort(_items, sortType, ascending).ToList();

        // 이미 정렬된 상태라면 이벤트를 발생시키지 않음
        _isSorting = true;
        try
        {
            // 1. 현재 인덱스 위치를 Dictionary에 캐싱 (O(N))
            var indexMap = new Dictionary<FileItem, int>(_items.Count);
            for (int i = 0; i < _items.Count; i++)
            {
                indexMap[_items[i]] = i;
            }

            // 2. 목표 순서(sortedItems)대로 현재 컬렉션의 위치를 조정
            for (int targetIdx = 0; targetIdx < sortedItems.Count; targetIdx++)
            {
                var item = sortedItems[targetIdx];
                int currentIdx = indexMap[item];

                if (currentIdx != targetIdx)
                {
                    // ObservableCollection.Move를 통해 최소한의 UI 변경 이벤트만 발생시킴
                    _items.Move(currentIdx, targetIdx);

                    // Move에 의해 위치가 바뀐 구간의 인덱스 정보를 갱신 (O(N) 미만)
                    int lo = Math.Min(currentIdx, targetIdx);
                    int hi = Math.Max(currentIdx, targetIdx);
                    for (int j = lo; j <= hi; j++)
                    {
                        indexMap[_items[j]] = j;
                    }
                }
            }
        }
        finally
        {
            _isSorting = false;
            // 정렬 완료 후 통계를 딱 한 번만 갱신
            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(UnsupportedCount));
        }
    }

    /// <summary>
    /// 선택된 아이템들을 목록에서 제거합니다.
    /// </summary>
    /// <param name="itemsToRemove">제거할 아이템 목록</param>
    /// <returns>실제로 제거된 아이템의 개수</returns>
    public int RemoveItems(IEnumerable<FileItem> itemsToRemove)
    {
        if (itemsToRemove == null) return 0;

        int count = 0;
        var itemList = itemsToRemove.ToList();
        foreach (var item in itemList)
        {
            if (_items.Remove(item))
            {
                _pathSet.Remove(item.Path);
                count++;
            }
        }
        return count;
    }

    /// <summary>
    /// 현재 목록에 표시된 순서대로 모든 아이템의 '추가 순번(AddIndex)'을 재설정합니다.
    /// </summary>
    public void ReorderIndex()
    {
        int index = 1;
        foreach (var item in _items)
        {
            item.AddIndex = index++;
        }
        _nextAddIndex = index;
    }

    /// <summary>
    /// 드래그 앤 드롭 등으로 목록 내에서 아이템들의 위치를 수동으로 이동시킵니다.
    /// </summary>
    /// <param name="itemsToMove">이동할 아이템들</param>
    /// <param name="targetIndex">기준 위치 인덱스</param>
    /// <param name="isBottom">기준 위치의 아래쪽에 배치할지 여부</param>
    public void MoveItems(List<FileItem> itemsToMove, int targetIndex, bool isBottom)
    {
        if (itemsToMove == null || itemsToMove.Count == 0) return;

        // 이동 대상 근처의 타겟 아이템 확보
        FileItem? targetItem = (targetIndex >= 0 && targetIndex < _items.Count) ? _items[targetIndex] : null;

        // 1. 이동할 대상들만 추출하여 현재 리스트에서 제거
        var actualMoving = itemsToMove.Where(i => _items.Contains(i)).ToList();
        foreach (var item in actualMoving) _items.Remove(item);

        // 2. 최종 삽입 위치 결정
        int insertIndex;
        if (targetItem != null && _items.Contains(targetItem))
        {
            insertIndex = _items.IndexOf(targetItem);
            if (isBottom) insertIndex++;
        }
        else
        {
            insertIndex = Math.Min(targetIndex, _items.Count);
        }

        // 3. 새 위치에 순차적으로 삽입
        for (int i = 0; i < actualMoving.Count; i++)
        {
            _items.Insert(Math.Min(insertIndex + i, _items.Count), actualMoving[i]);
        }
    }

    private void MoveItems(MoveItemsRequest? request)
    {
        if (request == null) return;

        MoveItems(request.ItemsToMove.ToList(), request.TargetIndex, request.IsBottom);
    }
}
