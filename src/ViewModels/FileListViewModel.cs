using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using PixConvert.Models;
using PixConvert.Services;

namespace PixConvert.ViewModels;

/// <summary>
/// 파일 목록 데이터의 관리와 조작(추가, 삭제, 정렬, 이동 등)을 담당하는 뷰모델입니다.
/// </summary>
public class FileListViewModel
{
    private readonly ObservableCollection<FileItem> _items = new();
    // 중복 체크용 HashSet (O(1) 탐색, 대소문자 무시)
    private readonly HashSet<string> _pathSet = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>화면에 바인딩되는 읽기 전용 파일 아이템 컬렉션입니다.</summary>
    public ReadOnlyObservableCollection<FileItem> Items { get; }

    // 다음에 추가될 아이템의 기본 순번
    private int _nextAddIndex = 1;

    public FileListViewModel()
    {
        Items = new ReadOnlyObservableCollection<FileItem>(_items);
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
    /// <param name="option">정렬 기준</param>
    /// <param name="ascending">오름차순 여부</param>
    public void Sorting(ISortingService sortingService, SortOption option, bool ascending)
    {
        if (_items.Count == 0) return;

        var sortedItems = sortingService.Sort(_items, option, ascending).ToList();

        // UI 갱신을 위해 컬렉션을 재구성합니다.
        _items.Clear();
        foreach (var item in sortedItems)
        {
            _items.Add(item);
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
}
