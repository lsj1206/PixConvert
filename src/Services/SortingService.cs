using System.Linq;
using System.Collections.Generic;
using PixConvert.Models;

namespace PixConvert.Services;

/// <summary>
/// 파일 목록을 다양한 조건(이름, 크기, 날짜 등)에 따라 정렬하는 서비스입니다.
/// </summary>
public class SortingService : ISortingService
{
    // [성능 최적화] 비교자 인스턴스를 재사용하여 가비지 컬렉션 부담을 줄임
    private static readonly WindowsNaturalComparer _naturalComparer = new();

    /// <summary>
    /// 지정된 정렬 옵션과 방향에 따라 파일 목록을 정렬합니다.
    /// </summary>
    public IEnumerable<FileItem> Sort(IEnumerable<FileItem> items, SortOption option, bool isAscending)
    {
        var itemList = items.ToList();
        if (itemList.Count == 0) return itemList;

        IOrderedEnumerable<FileItem> sortedItems;

        // 1차 정렬: 사용자가 선택한 주 정렬 기준
        switch (option.Type)
        {
            case SortType.NameIndex:
            case SortType.NamePath:
                // 윈도우 탐색기 방식의 자연스러운 정렬(숫자 인식 등) 적용
                sortedItems = isAscending
                    ? itemList.OrderBy(x => x.BaseName, _naturalComparer)
                    : itemList.OrderByDescending(x => x.BaseName, _naturalComparer);
                break;
            case SortType.PathIndex:
            case SortType.PathName:
                sortedItems = isAscending
                    ? itemList.OrderBy(x => x.Directory, _naturalComparer)
                    : itemList.OrderByDescending(x => x.Directory, _naturalComparer);
                break;
            case SortType.Size:
                sortedItems = isAscending
                    ? itemList.OrderBy(x => x.Size)
                    : itemList.OrderByDescending(x => x.Size);
                break;
            case SortType.CreatedDate:
                sortedItems = isAscending
                    ? itemList.OrderBy(x => x.CreatedDate)
                    : itemList.OrderByDescending(x => x.CreatedDate);
                break;
            case SortType.ModifiedDate:
                sortedItems = isAscending
                    ? itemList.OrderBy(x => x.ModifiedDate)
                    : itemList.OrderByDescending(x => x.ModifiedDate);
                break;
            case SortType.AddIndex:
            default:
                // 목록에 추가된 순서(AddIndex)대로 정렬
                sortedItems = isAscending
                    ? itemList.OrderBy(x => x.AddIndex)
                    : itemList.OrderByDescending(x => x.AddIndex);
                break;
        }

        // 2차 정렬: 1차 기준이 같을 때 적용되는 보조 정렬 기준
        switch (option.Type)
        {
            case SortType.NameIndex:
            case SortType.PathIndex:
            case SortType.Size:
            case SortType.CreatedDate:
            case SortType.ModifiedDate:
                // 대부분의 경우 중복 시 번호(AddIndex) 순으로 정렬하여 일관성 유지
                sortedItems = isAscending
                    ? sortedItems.ThenBy(x => x.AddIndex)
                    : sortedItems.ThenByDescending(x => x.AddIndex);
                break;
            case SortType.NamePath:
                // 이름이 같을 경우 경로(Path) 순으로 정렬
                sortedItems = isAscending
                    ? sortedItems.ThenBy(x => x.Path, _naturalComparer)
                    : sortedItems.ThenByDescending(x => x.Path, _naturalComparer);
                break;
            case SortType.PathName:
                // 경로가 같을 경우 이름(BaseName) 순으로 정렬
                sortedItems = isAscending
                    ? sortedItems.ThenBy(x => x.BaseName, _naturalComparer)
                    : sortedItems.ThenByDescending(x => x.BaseName, _naturalComparer);
                break;
        }

        return sortedItems;
    }
}
