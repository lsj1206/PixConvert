using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Moq;
using PixConvert.Models;
using PixConvert.Services;
using PixConvert.ViewModels;
using Xunit;

namespace PixConvert.Tests;

/// <summary>
/// FileListViewModel(파일 목록 컬렉션 관리) 로직을 검증하는 테스트 클래스입니다.
/// [Mock 구성]
/// FileListViewModel은 ILanguageService와 ILogger를 의존성으로 받지만,
/// 핵심 로직(AddItem, RemoveItems 등)은 컬렉션 조작이 전부이므로
/// 두 의존성 모두 기본 Mock으로만 주입하고 별도 Setup 없이 테스트합니다.
/// </summary>
public class FileListViewModelTests
{
    private readonly Mock<ILanguageService> _mockLang;
    private readonly Mock<ILogger<FileListViewModel>> _mockLogger;

    // 테스트 대상(SUT): FileListViewModel 인스턴스
    private readonly FileListViewModel _vm;

    public FileListViewModelTests()
    {
        // Moq 기본 Mock 생성 — 각 인터페이스의 메서드는 기본값(null/0/false)을 반환
        _mockLang = new Mock<ILanguageService>();
        _mockLogger = new Mock<ILogger<FileListViewModel>>();

        // 의존성 주입으로 순수 단위 테스트 환경 구성
        _vm = new FileListViewModel(_mockLang.Object, _mockLogger.Object);
    }

    // ─────────────────────────────────────────────────
    // AddItem 테스트
    // ─────────────────────────────────────────────────

    /// <summary>
    /// 시나리오: 서로 다른 경로를 가진 파일 2개를 순서대로 추가.
    /// 검증 목표: 각 파일이 성공적으로 추가되고, AddIndex가 1→2 순으로 자동 증가하는지 확인.
    ///            AddIndex는 사용자가 파일을 추가한 순서를 기록하는 추가 순번입니다.
    /// </summary>
    [Fact]
    public void AddItem_WhenNewPath_ShouldSucceedAndIncrementIndex()
    {
        // Arrange: 서로 다른 경로를 가진 두 FileItem 생성
        var item1 = new FileItem { Path = "C:\\file1.jpg" };
        var item2 = new FileItem { Path = "C:\\file2.jpg" };

        // Act: 두 파일을 순서대로 추가
        bool result1 = _vm.AddItem(item1);
        bool result2 = _vm.AddItem(item2);

        // Assert: 반환값이 true(추가 성공)인지 확인
        Assert.True(result1);
        Assert.True(result2);
        // Assert: 첫 번째 파일은 AddIndex=1, 두 번째는 AddIndex=2로 증가했는지 확인
        Assert.Equal(1, item1.AddIndex);
        Assert.Equal(2, item2.AddIndex);
        // Assert: 컬렉션 전체 수가 2개인지 최종 확인
        Assert.Equal(2, _vm.Items.Count);

        // Assert: PathSet에도 동일한 경로가 존재하는지 확인 (v4 추가)
        Assert.Contains("C:\\file1.jpg", _vm.PathSet);
        Assert.Contains("C:\\file2.jpg", _vm.PathSet);
    }

    /// <summary>
    /// 시나리오: 동일한 경로로 파일을 두 번 추가 시도.
    /// 검증 목표: 내부 HashSet이 중복 경로를 감지하여 두 번째 추가를 거부하는지 확인.
    ///            → 파일 목록에 같은 파일이 두 번 나타나는 UI 현상을 방지합니다.
    /// </summary>
    [Fact]
    public void AddItem_WhenDuplicatePath_ShouldReturnFalse()
    {
        // Arrange: 첫 번째 추가는 성공
        var item = new FileItem { Path = "C:\\duplicate.jpg" };
        _vm.AddItem(item);

        // Act: 완전히 동일한 경로로 두 번째 추가 시도
        var duplicate = new FileItem { Path = "C:\\duplicate.jpg" };
        bool result = _vm.AddItem(duplicate);

        // Assert: 중복이므로 false 반환 확인
        Assert.False(result);
        // Assert: 목록은 여전히 1개 유지 (중복이 추가되지 않았음을 확인)
        Assert.Single(_vm.Items);
    }

    /// <summary>
    /// 시나리오: 대소문자만 다른 동일 경로로 추가 시도.
    /// 검증 목표: 내부 HashSet이 OrdinalIgnoreCase 비교자로 구성되어
    ///            "C:\Photo.jpg"와 "C:\PHOTO.JPG"를 같은 경로로 처리하는지 확인.
    ///            Windows 파일시스템은 대소문자를 구별하지 않으므로 필수 검증입니다.
    /// </summary>
    [Fact]
    public void AddItem_WhenDuplicatePathWithDifferentCase_ShouldReturnFalse()
    {
        // Arrange: 소문자 경로 먼저 추가
        var item = new FileItem { Path = "C:\\Photo.jpg" };
        _vm.AddItem(item);

        // Act: 대문자 경로로 동일 파일 재추가 시도
        var duplicate = new FileItem { Path = "C:\\PHOTO.JPG" };
        bool result = _vm.AddItem(duplicate);

        // Assert: Windows 파일시스템 기준으로 동일 경로 → 중복 거부 확인
        Assert.False(result);
        Assert.Single(_vm.Items);
    }

    // ─────────────────────────────────────────────────
    // AddRange 테스트
    // ─────────────────────────────────────────────────

    /// <summary>
    /// 시나리오: 정상 파일 2개 + 중복 파일 1개가 섞인 배치를 AddRange로 추가.
    /// 검증 목표: AddRange가 내부적으로 AddItem을 각각 호출하여 중복을 필터링하고,
    ///            실제로 추가된 파일의 수만 정확히 반환하는지 확인.
    ///            → 드래그 앤 드롭 대량 추가 시 중복 자동 필터링 로직을 검증합니다.
    /// </summary>
    [Fact]
    public void AddRange_WhenMixedItems_ShouldReturnOnlyAddedCount()
    {
        // Arrange: 기존에 이미 1개 파일이 있는 상태
        _vm.AddItem(new FileItem { Path = "C:\\existing.jpg" });

        var newItems = new List<FileItem>
        {
            new() { Path = "C:\\existing.jpg" }, // 이미 존재하는 경로 → 중복
            new() { Path = "C:\\new1.jpg" },     // 새 파일 → 추가됨
            new() { Path = "C:\\new2.jpg" }      // 새 파일 → 추가됨
        };

        // Act: 3개 배치 추가 시도 (중복 1개 포함)
        int addedCount = _vm.AddRange(newItems);

        // Assert: 반환값 = 실제 추가 성공 수 (중복 제외 2개)
        Assert.Equal(2, addedCount);
        // Assert: 전체 목록은 기존 1 + 신규 2 = 3개
        Assert.Equal(3, _vm.Items.Count);
    }

    // ─────────────────────────────────────────────────
    // RemoveItems 테스트
    // ─────────────────────────────────────────────────

    /// <summary>
    /// 시나리오: 추가된 파일을 RemoveItems로 제거.
    /// 검증 목표 1: 제거 후 컬렉션 수가 0이 되는지 확인.
    /// 검증 목표 2: 내부 PathSet(HashSet)도 동기화되어 동일 경로를 다시 추가할 수 있는지 확인.
    ///              → 파일 제거 시 "이미 있는 파일"로 오탐하는 버그를 방지합니다.
    /// </summary>
    [Fact]
    public void RemoveItems_ShouldDecreaseCountAndAllowReAdd()
    {
        // Arrange: 파일 1개 추가
        var item = new FileItem { Path = "C:\\removable.jpg" };
        _vm.AddItem(item);
        Assert.Single(_vm.Items); // 사전 조건 검증

        // Act: 해당 파일 제거
        _vm.RemoveItems(new List<FileItem> { item });

        // Assert 1: 컬렉션이 비어있는지 확인
        Assert.Empty(_vm.Items);

        // Assert 2: 제거 후 동일 경로로 재추가 가능한지 확인 (PathSet 동기화 검증)
        //           HashSet에서 제거가 정상적으로 이루어졌다면 재추가 시 true를 반환해야 함
        var reAdded = new FileItem { Path = "C:\\removable.jpg" };
        bool result = _vm.AddItem(reAdded);
        Assert.True(result);
    }

    // ─────────────────────────────────────────────────
    // MoveItems 테스트
    // ─────────────────────────────────────────────────

    /// <summary>
    /// 시나리오: A→B→C 순서의 목록에서 C를 인덱스 0(최상단)으로 이동.
    /// 검증 목표: MoveItems 실행 후 컬렉션의 순서가 [C, A, B]가 되는지 확인.
    ///            결과 순서가 정확해야 사용자가 UI에서 올바른 순서를 볼 수 있습니다.
    /// </summary>
    [Fact]
    public void MoveItems_ToTop_ShouldPlaceItemAtIndexZero()
    {
        // Arrange: A(인덱스0) → B(인덱스1) → C(인덱스2) 순서로 추가
        var a = new FileItem { Path = "C:\\a.jpg" };
        var b = new FileItem { Path = "C:\\b.jpg" };
        var c = new FileItem { Path = "C:\\c.jpg" };
        _vm.AddItem(a);
        _vm.AddItem(b);
        _vm.AddItem(c);

        // Act: C를 목록 최상단(targetIndex=0)으로 이동
        //      isBottom=false → 지정 인덱스 위(앞)에 삽입
        _vm.MoveItems(new List<FileItem> { c }, 0, isBottom: false);

        // Assert: 이동 후 순서가 [C, A, B]인지 각 인덱스별로 검증
        Assert.Equal("C:\\c.jpg", _vm.Items[0].Path); // C가 맨 앞으로
        Assert.Equal("C:\\a.jpg", _vm.Items[1].Path); // A는 밀려서 두 번째
        Assert.Equal("C:\\b.jpg", _vm.Items[2].Path); // B는 밀려서 세 번째
    }

    // ─────────────────────────────────────────────────
    // ReorderIndex 테스트
    // ─────────────────────────────────────────────────

    /// <summary>
    /// 시나리오: 파일 순서 변경(MoveItems) 후 ReorderIndex 호출.
    /// 검증 목표: 현재 컬렉션의 표시 순서(Visual Order)에 맞춰
    ///            AddIndex가 1부터 순차적으로 재부여되는지 확인.
    ///            이 기능은 정렬 후 "#" 열(순번)이 1,2,3... 순서로 갱신되는 UI를 지원합니다.
    /// </summary>
    [Fact]
    public void ReorderIndex_ShouldReassignSequentiallyFromOne()
    {
        // Arrange: A(AddIndex=1) → B(AddIndex=2) → C(AddIndex=3) 순서로 추가
        var a = new FileItem { Path = "C:\\a.jpg" };
        var b = new FileItem { Path = "C:\\b.jpg" };
        var c = new FileItem { Path = "C:\\c.jpg" };
        _vm.AddItem(a);
        _vm.AddItem(b);
        _vm.AddItem(c);

        // MoveItems로 C를 맨 앞으로 이동 → 디스플레이 순서: [C, A, B]
        // 이 시점에서 AddIndex는 디스플레이 순서와 불일치 상태 (C=3, A=1, B=2)
        _vm.MoveItems(new List<FileItem> { c }, 0, isBottom: false);

        // Act: 현재 디스플레이 순서 기준으로 AddIndex를 1부터 재부여
        _vm.ReorderIndex();

        // Assert: 디스플레이 기준 [C, A, B]에 맞게 1, 2, 3이 재부여되었는지 확인
        Assert.Equal(1, _vm.Items[0].AddIndex); // C: 새로운 AddIndex=1
        Assert.Equal(2, _vm.Items[1].AddIndex); // A: 새로운 AddIndex=2
        Assert.Equal(3, _vm.Items[2].AddIndex); // B: 새로운 AddIndex=3
    }

    // ─────────────────────────────────────────────────
    // Clear 테스트
    // ─────────────────────────────────────────────────

    /// <summary>
    /// 시나리오: 파일 3개가 있는 상태에서 Clear 호출.
    /// 검증 목표 1: Clear 후 컬렉션이 완전히 비워지는지 확인.
    /// 검증 목표 2: Clear 후 AddIndex 카운터가 Reset되어,
    ///              재추가 시 AddIndex가 1부터 다시 시작하는지 확인.
    ///              또한 이전에 있던 경로도 재추가 가능한지(PathSet 초기화) 검증합니다.
    /// </summary>
    [Fact]
    public void Clear_ShouldResetAllStateAndRestartIndexFromOne()
    {
        // Arrange: 3개 파일 추가 (AddIndex: 1, 2, 3)
        _vm.AddItem(new FileItem { Path = "C:\\a.jpg" });
        _vm.AddItem(new FileItem { Path = "C:\\b.jpg" });
        _vm.AddItem(new FileItem { Path = "C:\\c.jpg" });
        Assert.Equal(3, _vm.Items.Count); // 사전 조건 확인

        // Act: 전체 초기화
        _vm.Clear();

        // Assert 1: 컬렉션이 비어있는지 확인
        Assert.Empty(_vm.Items);

        // Assert 2: Clear 후 동일 경로 재추가 가능 + AddIndex카운터가 1부터 재시작 확인
        var newItem = new FileItem { Path = "C:\\a.jpg" }; // 이전에 있던 경로도 재추가 가능해야 함
        _vm.AddItem(newItem);
        Assert.Equal(1, newItem.AddIndex); // Clear 후 첫 추가이므로 AddIndex=1이어야 함
    }

    // ─────────────────────────────────────────────────
    // 정렬 최적화 (Task E) 테스트
    // ─────────────────────────────────────────────────

    /// <summary>
    /// 시나리오: 이미 정렬된 상태에서 정렬 수행.
    /// 검증 목표: Move가 발생하지 않아야 함. (동일 인덱스 이동 방지 로직 검증)
    /// </summary>
    [Fact]
    public void Sorting_WhenAlreadySorted_ShouldFireZeroMoveEvents()
    {
        // Arrange
        var items = new[] { "A", "B", "C" }.Select(n => new FileItem { Path = $"C:\\{n}.jpg" }).ToList();
        foreach (var item in items) _vm.AddItem(item);

        var mockSort = new Mock<ISortingService>();
        mockSort.Setup(s => s.Sort(It.IsAny<IEnumerable<FileItem>>(), It.IsAny<SortType>(), It.IsAny<bool>()))
                .Returns(items); // 이미 정렬된 상태 그대로 반환

        int moveCount = 0;
        // _items(internal) 접근 대신 Items 프로퍼티를 ObservableCollection으로 캐스팅하여 구독
        if (_vm.Items is System.Collections.Specialized.INotifyCollectionChanged collection)
        {
            collection.CollectionChanged += (s, e) =>
            {
                if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Move)
                    moveCount++;
            };
        }

        // Act
        _vm.Sorting(mockSort.Object, SortType.NameIndex, true);

        // Assert
        Assert.Equal(0, moveCount);
        Assert.Equal(3, _vm.Items.Count);
        Assert.Equal("C:\\A.jpg", _vm.Items[0].Path);
    }

    /// <summary>
    /// 시나리오: 역순으로 정렬된 상태에서 정렬 수행.
    /// 검증 목표: Move를 통해 위치가 변경되고 최종 순서가 정확해야 함.
    /// </summary>
    [Fact]
    public void Sorting_WhenReversed_ShouldCorrectlyReorderUsingMove()
    {
        // Arrange
        var a = new FileItem { Path = "C:\\a.jpg" };
        var b = new FileItem { Path = "C:\\b.jpg" };
        var c = new FileItem { Path = "C:\\c.jpg" };
        _vm.AddItem(c); // 인덱스 0
        _vm.AddItem(b); // 인덱스 1
        _vm.AddItem(a); // 인덱스 2

        var sorted = new List<FileItem> { a, b, c };
        var mockSort = new Mock<ISortingService>();
        mockSort.Setup(s => s.Sort(It.IsAny<IEnumerable<FileItem>>(), It.IsAny<SortType>(), It.IsAny<bool>()))
                .Returns(sorted);

        // Act
        _vm.Sorting(mockSort.Object, SortType.NameIndex, true);

        // Assert
        Assert.Equal("C:\\a.jpg", _vm.Items[0].Path);
        Assert.Equal("C:\\b.jpg", _vm.Items[1].Path);
        Assert.Equal("C:\\c.jpg", _vm.Items[2].Path);
    }

    /// <summary>
    /// 시나리오: 정렬 도중(try 블록 내부) 예외 발생.
    /// 검증 목표: finally 블록을 통해 _isSorting 플래그가 반드시 false로 복구되어야 함.
    /// </summary>
    [Fact]
    public void Sorting_WhenExceptionInsideTryBlock_ShouldResetIsSortingFlag()
    {
        // Arrange
        var itemInList = new FileItem { Path = "C:\\in.jpg" };
        var itemNotInList = new FileItem { Path = "C:\\out.jpg" }; // 맵에 없는 아이템
        _vm.AddItem(itemInList);

        var mockSort = new Mock<ISortingService>();
        // Sort() 결과에 목록에 없는 아이템을 섞어서 반환 -> try 블록 내 indexMap 조회 시 KeyNotFoundException 유발
        mockSort.Setup(s => s.Sort(It.IsAny<IEnumerable<FileItem>>(), It.IsAny<SortType>(), It.IsAny<bool>()))
                .Returns(new List<FileItem> { itemNotInList });

        string? notifiedProperty = null;
        _vm.PropertyChanged += (s, e) => notifiedProperty = e.PropertyName;

        // Act & Assert
        // 1. 예외가 발생하는지 확인
        Assert.ThrowsAny<System.Exception>(() => _vm.Sorting(mockSort.Object, SortType.NameIndex, true));
        
        // 2. _isSorting이 false로 복구되었는지 Reflection으로 직접 검증
        var field = typeof(FileListViewModel).GetField("_isSorting", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(field);
        var rawValue = field!.GetValue(_vm);
        Assert.NotNull(rawValue);
        var isSortingValue = (bool)rawValue;
        Assert.False(isSortingValue);

        // 3. finally 블록에서 통계 갱신(OnPropertyChanged)이 호출되었는지 확인
        Assert.Equal(nameof(FileListViewModel.UnsupportedCount), notifiedProperty);
    }
}
