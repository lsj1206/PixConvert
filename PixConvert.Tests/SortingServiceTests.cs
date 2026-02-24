using System.Collections.Generic;
using System.Linq;
using PixConvert.Models;
using PixConvert.Services;
using Xunit; // xUnit 테스팅 프레임워크 네임스페이스

namespace PixConvert.Tests;

/// <summary>
/// SortingService(정렬 서비스)의 로직이 올바르게 작동하는지 검증하는 테스트 클래스입니다.
/// 유닛 테스트의 가장 기본인 3단계(AAA: Arrange, Act, Assert) 패턴을 따릅니다.
/// </summary>
public class SortingServiceTests
{
    // 테스트 대상이 되는 서비스 인스턴스 (System Under Test, SUT라고도 부릅니다)
    private readonly ISortingService _sortingService;

    /// <summary>
    /// 생성자: 각 테스트 메서드가 실행될 때마다 새롭게 호출되어 깨끗한 상태를 만듭니다.
    /// </summary>
    public SortingServiceTests()
    {
        _sortingService = new SortingService();
    }

    // [Fact] 속성은 이 메서드가 매개변수 없이 단독으로 실행되는 '단일 테스트 케이스'임을 xUnit 체계에 알려줍니다.
    [Fact]
    public void Sort_ByNameAscending_ShouldOrderAlphabetically()
    {
        // 1. Arrange (준비)
        // 테스트를 수행하기 위한 초기 데이터나 가짜 데이터를 셋업하는 단계입니다.
        // 여기서는 뒤죽박죽인 파일 이름 3개를 리스트에 넣습니다.
        var list = new List<FileItem>
        {
            new FileItem { Path = "Z_File.jpg" },
            new FileItem { Path = "A_File.jpg" },
            new FileItem { Path = "M_File.jpg" }
        };

        // 2. Act (실행)
        // 실제로 우리가 테스트하고자 하는 핵심 로직(함수)을 실행하는 단계입니다.
        // 이름 기준(NameIndex), 오름차순(true)으로 정렬하라고 명령합니다.
        var sortedList = _sortingService.Sort(list, new SortOption { Type = SortType.NameIndex }, true).ToList();

        // 3. Assert (검증)
        // 로직의 실행 결과가 우리가 예상한 것과 정확히 일치하는지 단언(Assert)하는 단계입니다.
        // 알파벳 오름차순이므로 첫 번째는 A, 두 번째는 M, 세 번째는 Z여야 합니다.
        // 다르면 여기서 테스트가 빨간불(Failed)을 내고 멈춥니다.
        Assert.Equal("A_File.jpg", sortedList[0].Path);
        Assert.Equal("M_File.jpg", sortedList[1].Path);
        Assert.Equal("Z_File.jpg", sortedList[2].Path);
    }

    [Fact]
    public void Sort_BySizeDescending_ShouldOrderLargestFirst()
    {
        // Arrange (준비)
        var list = new List<FileItem>
        {
            new FileItem { Path = "File1.jpg", Size = 100 },
            new FileItem { Path = "File2.jpg", Size = 500 },
            new FileItem { Path = "File3.jpg", Size = 50 }
        };

        // Act (실행)
        // 크기 기준(Size), 오름차순=false (즉, 내림차순)으로 정렬을 요청합니다.
        var sortedList = _sortingService.Sort(list, new SortOption { Type = SortType.Size }, false).ToList();

        // Assert (검증)
        // 내림차순이므로 용량이 가장 큰 500이 0번 인덱스, 가장 작은 50이 2번 인덱스여야 합니다.
        Assert.Equal(500, sortedList[0].Size);
        Assert.Equal(100, sortedList[1].Size);
        Assert.Equal(50, sortedList[2].Size);
    }

    [Fact]
    public void Sort_BySignatureAscending_ShouldOrderCorrectly()
    {
        // Arrange (준비)
        var list = new List<FileItem>
        {
            new FileItem { Path = "2.jpg", FileSignature = "png" },
            new FileItem { Path = "3.jpg", FileSignature = "bmp" },
            new FileItem { Path = "1.jpg", FileSignature = "jpg" }
        };

        // Act (실행)
        // 우리가 분석한 실제 파일 포맷(시그니처) 기준으로 오름차순(A-Z) 정렬합니다.
        var sortedList = _sortingService.Sort(list, new SortOption { Type = SortType.Signature }, true).ToList();

        // Assert (검증)
        // 알파벳 순서에 따라 b(bmp) -> j(jpg) -> p(png) 순으로 잘 묶였는지 확인합니다.
        Assert.Equal("bmp", sortedList[0].FileSignature);
        Assert.Equal("jpg", sortedList[1].FileSignature);
        Assert.Equal("png", sortedList[2].FileSignature);
    }
}
