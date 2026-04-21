using System.Collections.Generic;
using System.Linq;
using PixConvert.Models;
using PixConvert.Services;
using Xunit;

namespace PixConvert.Tests;

/// <summary>
/// SortingService(파일 목록 정렬 서비스) 로직을 검증하는 테스트 클래스입니다.
///
/// [테스트 핵심 전략: 순수 함수 검증]
/// SortingService는 외부 의존성(ILogger, ILanguageService 등)이 없는 순수 서비스입니다.
/// Mock 객체 없이 new SortingService()로 직접 생성하여 정렬 알고리즘만 집중 검증합니다.
///
/// [AAA 패턴]
/// 모든 테스트는 Arrange(준비) → Act(실행) → Assert(검증) 3단계 구조를 따릅니다.
///
/// [SortType 열거형 구조]
/// - NameIndex: 파일 이름 단일 정렬
/// - Size: 파일 크기 단일 정렬
/// - Signature: 시그니처(포맷) 단일 정렬
/// - AddIndex: 추가 순번 단일 정렬
/// - PathIndex: 폴더 경로 단일 정렬
/// - PathName: 폴더 경로 1차 → 파일 이름 2차 정렬
/// - NamePath: 파일 이름 1차 → 폴더 경로 2차 정렬
/// </summary>
public class SortingServiceTests
{
    // 테스트 대상(SUT). SortingService는 외부 의존성이 없어 직접 생성 가능합니다.
    private readonly ISortingService _sortingService;

    /// <summary>
    /// xUnit은 각 [Fact] 테스트 실행 전마다 이 생성자를 새로 호출합니다.
    /// 덕분에 각 테스트는 항상 새로운(오염되지 않은) SortingService 인스턴스로 시작합니다.
    /// </summary>
    public SortingServiceTests()
    {
        _sortingService = new SortingService();
    }

    // ─────────────────────────────────────────────────
    // 기본 정렬 케이스 (이름 / 크기 / 시그니처)
    // ─────────────────────────────────────────────────

    /// <summary>
    /// 시나리오: 이름이 뒤섞인 파일 3개를 이름 오름차순으로 정렬.
    /// 검증 목표: SortType.NameIndex + isAscending=true 시 알파벳 A→Z 순서가 보장되는지 확인.
    /// </summary>
    [Fact]
    public void Sort_ByNameAscending_ShouldOrderAlphabetically()
    {
        // Arrange: Z → A → M 순서로 뒤섞어 준비
        var list = new List<FileItem>
        {
            new FileItem { Path = "Z_File.jpg" },
            new FileItem { Path = "A_File.jpg" },
            new FileItem { Path = "M_File.jpg" }
        };

        // Act: 이름 기준 오름차순 정렬 요청
        var sortedList = _sortingService.Sort(list, SortType.NameIndex, true).ToList();

        // Assert: 알파벳 오름차순(A→M→Z)으로 재배열되었는지 각 인덱스별로 검증
        Assert.Equal("A_File.jpg", sortedList[0].Path); // 첫 번째 = A
        Assert.Equal("M_File.jpg", sortedList[1].Path); // 두 번째 = M
        Assert.Equal("Z_File.jpg", sortedList[2].Path); // 세 번째 = Z
    }

    /// <summary>
    /// 시나리오: 파일 크기가 다른 3개를 크기 내림차순으로 정렬.
    /// 검증 목표: SortType.Size + isAscending=false 시 용량이 큰 파일이 앞에 오는지 확인.
    ///            → "큰 파일 먼저" 보기 기능에 대한 검증입니다.
    /// </summary>
    [Fact]
    public void Sort_BySizeDescending_ShouldOrderLargestFirst()
    {
        // Arrange: 크기가 각각 100, 500, 50인 파일 목록
        var list = new List<FileItem>
        {
            new FileItem { Path = "File1.jpg", Size = 100 },
            new FileItem { Path = "File2.jpg", Size = 500 }, // 가장 큰 파일
            new FileItem { Path = "File3.jpg", Size = 50 }  // 가장 작은 파일
        };

        // Act: 크기 기준 내림차순 정렬 (isAscending=false)
        var sortedList = _sortingService.Sort(list, SortType.Size, false).ToList();

        // Assert: 내림차순이므로 500 → 100 → 50 순으로 정렬되었는지 확인
        Assert.Equal(500, sortedList[0].Size); // 가장 큰 파일이 첫 번째
        Assert.Equal(100, sortedList[1].Size);
        Assert.Equal(50, sortedList[2].Size);  // 가장 작은 파일이 마지막
    }

    /// <summary>
    /// 시나리오: 시그니처(파일 포맷)가 다른 파일 3개를 시그니처 오름차순으로 정렬.
    /// 검증 목표: SortType.Signature + isAscending=true 시 시그니처 문자열 기준 A→Z 정렬 확인.
    ///            → 같은 포맷끼리 묶어보는 "포맷별 그룹 보기" 기능에 대한 검증입니다.
    /// </summary>
    [Fact]
    public void Sort_BySignatureAscending_ShouldOrderCorrectly()
    {
        // Arrange: 알파벳 순서가 뒤섞인 시그니처 준비 (png, bmp, jpg)
        var list = new List<FileItem>
        {
            new FileItem { Path = "2.jpg", FileSignature = "png" },
            new FileItem { Path = "3.jpg", FileSignature = "bmp" },
            new FileItem { Path = "1.jpg", FileSignature = "jpg" }
        };

        // Act: 시그니처 기준 오름차순 정렬
        var sortedList = _sortingService.Sort(list, SortType.Signature, true).ToList();

        // Assert: 알파벳 순서 b(bmp) → j(jpg) → p(png)로 정렬되었는지 확인
        Assert.Equal("bmp", sortedList[0].FileSignature);
        Assert.Equal("jpg", sortedList[1].FileSignature);
        Assert.Equal("png", sortedList[2].FileSignature);
    }

    // ─────────────────────────────────────────────────
    // 추가 정렬 케이스 (AddIndex / PathIndex / 내림차순 / 2차 정렬)
    // ─────────────────────────────────────────────────

    /// <summary>
    /// 시나리오: AddIndex가 뒤섞인 파일 3개를 AddIndex 오름차순으로 정렬.
    /// 검증 목표: SortType.AddIndex가 파일 추가 순서(1→2→3)로 목록을 복원하는지 확인.
    ///            → UI의 "추가 순서" 정렬 버튼 기능에 대한 검증입니다.
    /// </summary>
    [Fact]
    public void Sort_ByAddIndex_ShouldOrderByAdditionOrder()
    {
        // Arrange: AddIndex가 3→1→2 순으로 뒤섞인 상태
        var list = new List<FileItem>
        {
            new FileItem { Path = "C:\\b.jpg", AddIndex = 3 },
            new FileItem { Path = "C:\\a.jpg", AddIndex = 1 }, // 가장 먼저 추가된 파일
            new FileItem { Path = "C:\\c.jpg", AddIndex = 2 }
        };

        // Act: AddIndex 기준 오름차순 정렬 (추가된 순서대로 복원)
        var sorted = _sortingService.Sort(list, SortType.AddIndex, true).ToList();

        // Assert: 추가 순서인 1→2→3 순으로 정렬되었는지 확인
        Assert.Equal(1, sorted[0].AddIndex); // 가장 먼저 추가된 것이 첫 번째
        Assert.Equal(2, sorted[1].AddIndex);
        Assert.Equal(3, sorted[2].AddIndex);
    }

    /// <summary>
    /// 시나리오: 폴더 경로(Directory)가 다른 파일 3개를 경로 오름차순으로 정렬.
    /// 검증 목표: SortType.PathIndex가 파일 이름이 아닌 폴더 경로 기준으로 정렬하는지 확인.
    ///            → 파일 원본 위치(폴더 경로) 기준 "같은 폴더 묶기" 기능에 대한 검증입니다.
    /// </summary>
    [Fact]
    public void Sort_ByPathAscending_ShouldOrderByDirectory()
    {
        // Arrange: 파일 이름은 모두 "file.jpg"로 같고, 폴더 경로만 다른 상황
        var list = new List<FileItem>
        {
            new FileItem { Path = "C:\\Folder_Z\\file.jpg" },
            new FileItem { Path = "C:\\Folder_A\\file.jpg" },
            new FileItem { Path = "C:\\Folder_M\\file.jpg" }
        };

        // Act: 폴더 경로 기준 오름차순 정렬
        var sorted = _sortingService.Sort(list, SortType.PathIndex, true).ToList();

        // Assert: 폴더명 오름차순(A→M→Z)으로 정렬되었는지 확인
        Assert.Equal("C:\\Folder_A\\file.jpg", sorted[0].Path);
        Assert.Equal("C:\\Folder_M\\file.jpg", sorted[1].Path);
        Assert.Equal("C:\\Folder_Z\\file.jpg", sorted[2].Path);
    }

    /// <summary>
    /// 시나리오: 파일 이름을 내림차순으로 정렬.
    /// 검증 목표: isAscending=false 매개변수가 올바르게 작동하여 Z→A 역순을 보장하는지 확인.
    ///            기존 오름차순 테스트의 반전 케이스로, isAscending 플래그 자체를 검증합니다.
    /// </summary>
    [Fact]
    public void Sort_ByNameDescending_ShouldReverseAlphabeticalOrder()
    {
        // Arrange: 알파벳 A, Z, M 순으로 준비
        var list = new List<FileItem>
        {
            new FileItem { Path = "C:\\A_File.jpg" },
            new FileItem { Path = "C:\\Z_File.jpg" },
            new FileItem { Path = "C:\\M_File.jpg" }
        };

        // Act: 이름 기준 내림차순 (isAscending=false) 정렬
        var sorted = _sortingService.Sort(list, SortType.NameIndex, false).ToList();

        // Assert: 내림차순이므로 Z→M→A 순서여야 함
        Assert.Equal("C:\\Z_File.jpg", sorted[0].Path); // Z가 첫 번째
        Assert.Equal("C:\\M_File.jpg", sorted[1].Path);
        Assert.Equal("C:\\A_File.jpg", sorted[2].Path); // A가 마지막
    }

    /// <summary>
    /// 시나리오: 이름이 동일한 파일들을 NameIndex로 정렬 시 2차 정렬 동작 확인.
    /// 검증 목표: 1차 정렬 기준(이름)이 같을 때 2차 정렬 기준(AddIndex)이 자동으로 적용되는지 확인.
    ///            LINQ의 ThenBy() 체인이 올바르게 구현되어 있어야 통과합니다.
    ///            → 동명 파일 처리 시 목록 순서가 무작위로 뒤섞이는 버그 방지에 대한 검증입니다.
    /// </summary>
    [Fact]
    public void Sort_Secondary_WhenNameTied_ShouldSortByAddIndex()
    {
        // Arrange: 이름(파일명)이 모두 "Same.jpg"로 동일 → 이름으로만은 순서 결정 불가
        var list = new List<FileItem>
        {
            new FileItem { Path = "C:\\Same.jpg", AddIndex = 3 },
            new FileItem { Path = "C:\\Same.jpg", AddIndex = 1 }, // AddIndex가 가장 낮음
            new FileItem { Path = "C:\\Same.jpg", AddIndex = 2 }
        };

        // Act: 이름 기준 정렬 → 동일 이름이므로 2차 기준(AddIndex)으로 자동 정렬
        var sorted = _sortingService.Sort(list, SortType.NameIndex, true).ToList();

        // Assert: AddIndex 오름차순(1→2→3)으로 2차 정렬이 적용되었는지 확인
        Assert.Equal(1, sorted[0].AddIndex); // AddIndex=1이 첫 번째
        Assert.Equal(2, sorted[1].AddIndex);
        Assert.Equal(3, sorted[2].AddIndex);
    }

    /// <summary>
    /// 시나리오: 동일 폴더 내 파일들을 PathName 정렬.
    /// 검증 목표: SortType.PathName은 폴더 경로를 1차 기준으로, 파일 이름을 2차 기준으로 정렬하는지 확인.
    ///            같은 폴더 안의 파일은 이름순으로 세부 정렬됩니다.
    /// </summary>
    [Fact]
    public void Sort_ByPathName_WhenPathTied_ShouldSortByName()
    {
        // Arrange: 폴더 경로가 모두 "C:\Folder"로 같고, 파일 이름만 다른 상황
        var list = new List<FileItem>
        {
            new FileItem { Path = "C:\\Folder\\Z_img.jpg" },
            new FileItem { Path = "C:\\Folder\\A_img.jpg" },
            new FileItem { Path = "C:\\Folder\\M_img.jpg" }
        };

        // Act: PathName 정렬 (1차: 폴더 경로, 2차: 파일 이름)
        var sorted = _sortingService.Sort(list, SortType.PathName, true).ToList();

        // Assert: 경로가 같으므로 파일 이름 오름차순(A→M→Z)으로 정렬되었는지 확인
        Assert.Equal("C:\\Folder\\A_img.jpg", sorted[0].Path); // 이름 A가 첫 번째
        Assert.Equal("C:\\Folder\\M_img.jpg", sorted[1].Path);
        Assert.Equal("C:\\Folder\\Z_img.jpg", sorted[2].Path); // 이름 Z가 마지막
    }

    /// <summary>
    /// 시나리오: 파일 이름이 같고 폴더 경로만 다른 파일들을 NamePath 정렬.
    /// 검증 목표: SortType.NamePath는 파일 이름을 1차 기준으로, 폴더 경로를 2차 기준으로 정렬하는지 확인.
    ///            같은 이름의 파일은 경로순으로 세부 정렬됩니다.
    /// </summary>
    [Fact]
    public void Sort_ByNamePath_WhenNameTied_ShouldSortByPath()
    {
        // Arrange: 파일 이름이 모두 "same.jpg"로 같고, 폴더 경로만 다른 상황
        var list = new List<FileItem>
        {
            new FileItem { Path = "C:\\Folder_Z\\same.jpg" },
            new FileItem { Path = "C:\\Folder_A\\same.jpg" },
            new FileItem { Path = "C:\\Folder_M\\same.jpg" }
        };

        // Act: NamePath 정렬 (1차: 파일 이름, 2차: 폴더 경로)
        var sorted = _sortingService.Sort(list, SortType.NamePath, true).ToList();

        // Assert: 이름이 같으므로 폴더 경로 오름차순(A→M→Z)으로 정렬되었는지 확인
        Assert.Equal("C:\\Folder_A\\same.jpg", sorted[0].Path); // 경로 A가 첫 번째
        Assert.Equal("C:\\Folder_M\\same.jpg", sorted[1].Path);
        Assert.Equal("C:\\Folder_Z\\same.jpg", sorted[2].Path); // 경로 Z가 마지막
    }

    /// <summary>
    /// 시나리오: 빈 컬렉션을 Sort에 전달.
    /// 검증 목표: 입력이 비어있을 때 NullReferenceException 없이 빈 컬렉션을 안전하게 반환하는지 확인.
    ///            SortingService 내의 early return(조기 반환) 방어 코드가 작동하는지 검증합니다.
    /// </summary>
    [Fact]
    public void Sort_WhenEmptyList_ShouldReturnEmptyListWithoutError()
    {
        // Arrange: 아무것도 없는 빈 리스트
        var emptyList = new List<FileItem>();

        // Act: 빈 리스트를 정렬에 투입 (예외가 발생하지 않아야 함)
        var result = _sortingService.Sort(emptyList, SortType.NameIndex, true).ToList();

        // Assert: 예외 없이 빈 결과를 반환하는지 확인
        Assert.Empty(result);
    }
}
