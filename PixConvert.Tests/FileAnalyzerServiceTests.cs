using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using PixConvert.Models;
using PixConvert.Services;
using Xunit;

namespace PixConvert.Tests;

/// <summary>
/// FileAnalyzerService의 복합적인 파일 분석 로직(한도, 중복, 폴더 스캔 등)을 검증하는 테스트입니다.
/// </summary>
public class FileAnalyzerServiceTests
{
    private readonly Mock<IFileScannerService> _mockScanner;
    private readonly Mock<ILogger<FileAnalyzerService>> _mockLogger;
    private readonly Mock<ILanguageService> _mockLang;
    private readonly Mock<IDriveInfoService> _mockDriveInfo; // 추가
    private readonly FileAnalyzerService _service;

    public FileAnalyzerServiceTests()
    {
        _mockScanner = new Mock<IFileScannerService>();
        _mockLogger = new Mock<ILogger<FileAnalyzerService>>();
        _mockLang = new Mock<ILanguageService>();
        _mockDriveInfo = new Mock<IDriveInfoService>(); // 추가

        _mockLang.Setup(x => x.GetString(It.IsAny<string>())).Returns((string key) => key);

        // 테스트 시 병렬도를 1로 고정하여 결정론적 결과 확인
        _mockDriveInfo.Setup(x => x.GetOptimalParallelismAsync(It.IsAny<string>()))
            .ReturnsAsync(1); // 추가

        _service = new FileAnalyzerService(
            _mockScanner.Object, 
            _mockLogger.Object, 
            _mockLang.Object, 
            _mockDriveInfo.Object); // 서비스 추가
    }

    [Fact]
    public async Task ProcessPathsAsync_WhenUnderLimit_ShouldAddAllFiles()
    {
        // Arrange
        var paths = new[] { "C:\\test1.png", "C:\\test2.png" };
        int maxLimit = 10000;
        int currentCount = 0;

        _mockScanner.Setup(s => s.CreateFileItemAsync(It.IsAny<string>()))
            .ReturnsAsync((string p) => new FileItem { Path = p });

        // Act
        var result = await _service.ProcessPathsAsync(paths, maxLimit, currentCount);

        // Assert
        Assert.Equal(2, result.SuccessCount);
        Assert.Equal(0, result.IgnoredCount);
        Assert.Equal(0, result.DuplicateCount);
        Assert.Equal(2, result.NewItems.Count);
    }

    [Fact]
    public async Task ProcessPathsAsync_WithDuplicates_ShouldSkipAndNotConsumeCapacity()
    {
        // Arrange
        var paths = new[] { "C:\\dup.png", "C:\\new.png" };
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "C:\\dup.png" };
        int maxLimit = 10000;
        int currentCount = 0;

        _mockScanner.Setup(s => s.CreateFileItemAsync("C:\\new.png"))
            .ReturnsAsync(new FileItem { Path = "C:\\new.png" });

        // Act
        var result = await _service.ProcessPathsAsync(paths, maxLimit, currentCount, existing);

        // Assert
        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(1, result.DuplicateCount);
        Assert.Equal(0, result.IgnoredCount);
        Assert.Single(result.NewItems);
        Assert.Equal("C:\\new.png", result.NewItems[0].Path);
    }

    [Fact]
    public async Task ProcessPathsAsync_WhenOverLimit_ShouldPartiallyAdd()
    {
        // Arrange
        var paths = new[] { "C:\\1.png", "C:\\2.png", "C:\\3.png" };
        int maxLimit = 2; // 한도 2개
        int currentCount = 0;

        _mockScanner.Setup(s => s.CreateFileItemAsync(It.IsAny<string>()))
            .ReturnsAsync((string p) => new FileItem { Path = p });

        // Act
        var result = await _service.ProcessPathsAsync(paths, maxLimit, currentCount);

        // Assert
        Assert.Equal(2, result.SuccessCount);
        Assert.Equal(1, result.IgnoredCount); // 3개 중 1개 누락
        Assert.Equal(2, result.NewItems.Count);
    }

    [Fact]
    public async Task ProcessPathsAsync_WhenAlreadyAtLimit_ShouldReturnZeroSuccess()
    {
        // Arrange
        var paths = new[] { "C:\\test.png" };
        int maxLimit = 100;
        int currentCount = 100; // 이미 꽉 탐

        // Act
        var result = await _service.ProcessPathsAsync(paths, maxLimit, currentCount);

        // Assert
        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(1, result.IgnoredCount);
    }

    [Fact]
    public async Task ProcessPathsAsync_FolderScan_ShouldObeyLimit()
    {
        // Arrange
        string folderPath = "C:\\TestFolder";
        // 실제 디렉토리가 존재한다고 가정하기 위해 Directory.Exists 우회는 어렵지만,
        // FileScannerService.GetFilesInFolder를 Mocking하여 동작 시뮬레이션
        var paths = new[] { folderPath };
        int maxLimit = 5;
        int currentCount = 3; // 남은 공간 2개

        var fakeFiles = new List<FileInfo>
        {
            new FileInfo("C:\\TestFolder\\f1.png"),
            new FileInfo("C:\\TestFolder\\f2.png"),
            new FileInfo("C:\\TestFolder\\f3.png")
        };

        // Directory.Exists(folderPath)가 true를 반환해야 하므로 실제 임시 폴더 생성 필요
        string realTempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(realTempFolder);
        try
        {
            _mockScanner.Setup(s => s.GetFilesInFolder(realTempFolder))
                .Returns(fakeFiles);

            _mockScanner.Setup(s => s.CreateFileItemAsync(It.IsAny<string>()))
                .ReturnsAsync((string p) => new FileItem { Path = p });

            // Act
            var result = await _service.ProcessPathsAsync(new[] { realTempFolder }, maxLimit, currentCount);

            // Assert
            Assert.Equal(2, result.SuccessCount); // 남은 공간 2개만큼만 성공
            Assert.Equal(1, result.IgnoredCount); // 1개는 한도초과 무시
        }
        finally
        {
            if (Directory.Exists(realTempFolder)) Directory.Delete(realTempFolder);
        }
    }

    [Fact]
    public async Task ProcessPathsAsync_MixedDrives_ShouldQueryParallelismByFirstSeenDriveOrder()
    {
        // Arrange: D -> C -> D -> E 순서로 입력
        var paths = new[]
        {
            @"D:\d1.png",
            @"C:\c1.png",
            @"D:\d2.png",
            @"E:\e1.png"
        };

        var queriedPaths = new List<string>();
        _mockDriveInfo
            .Setup(x => x.GetOptimalParallelismAsync(It.IsAny<string>()))
            .Callback<string>(p => queriedPaths.Add(p))
            .ReturnsAsync(1);

        _mockScanner
            .Setup(s => s.CreateFileItemAsync(It.IsAny<string>()))
            .ReturnsAsync((string p) => new FileItem { Path = p });

        // Act
        var result = await _service.ProcessPathsAsync(paths, maxItemCount: 100, currentCount: 0);

        // Assert: 드라이브 그룹 대표 경로가 최초 등장 순서대로 조회되는지 확인
        Assert.Equal(new[] { @"D:\d1.png", @"C:\c1.png", @"E:\e1.png" }, queriedPaths);
        Assert.Equal(4, result.SuccessCount);

        // 결과 목록 순서도 입력 순서를 유지해야 함
        Assert.Equal(paths, result.NewItems.Select(x => x.Path).ToArray());
    }

    [Fact]
    public async Task ProcessPathsAsync_SingleDrive_ShouldQueryParallelismOnlyOnce()
    {
        // Arrange: 모두 C 드라이브
        var paths = new[]
        {
            @"C:\a.png",
            @"C:\b.png",
            @"C:\c.png"
        };

        int queryCount = 0;
        string? firstQueryPath = null;
        _mockDriveInfo
            .Setup(x => x.GetOptimalParallelismAsync(It.IsAny<string>()))
            .Callback<string>(p =>
            {
                queryCount++;
                firstQueryPath ??= p;
            })
            .ReturnsAsync(1);

        _mockScanner
            .Setup(s => s.CreateFileItemAsync(It.IsAny<string>()))
            .ReturnsAsync((string p) => new FileItem { Path = p });

        // Act
        var result = await _service.ProcessPathsAsync(paths, maxItemCount: 100, currentCount: 0);

        // Assert
        Assert.Equal(1, queryCount);
        Assert.Equal(@"C:\a.png", firstQueryPath);
        Assert.Equal(3, result.SuccessCount);
    }
}
