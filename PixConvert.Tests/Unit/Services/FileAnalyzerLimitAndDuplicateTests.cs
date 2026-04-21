using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using PixConvert.Models;
using Xunit;

namespace PixConvert.Tests;

public sealed class FileAnalyzerLimitAndDuplicateTests : IDisposable
{
    private readonly FileAnalyzerServiceTestHarness _harness = new();

    [Fact]
    public async Task ProcessPathsAsync_WithDuplicates_ShouldSkipAndNotConsumeCapacity()
    {
        string[] paths = ["C:\\dup.png", "C:\\new.png"];
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "C:\\dup.png" };

        _harness.Scanner
            .Setup(service => service.CreateFileItemAsync("C:\\new.png"))
            .ReturnsAsync(new FileItem { Path = "C:\\new.png" });

        var result = await _harness.Service.ProcessPathsAsync(paths, maxItemCount: 10000, currentCount: 0, existing);

        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(1, result.DuplicateCount);
        Assert.Equal(0, result.IgnoredCount);
        Assert.Single(result.NewItems);
        Assert.Equal("C:\\new.png", result.NewItems[0].Path);
    }

    public static IEnumerable<object[]> EquivalentBatchPaths()
    {
        yield return new object[] { new[] { "C:\\dup.png", "C:\\dup.png" }, "C:\\dup.png" };
        yield return new object[] { new[] { "C:\\Photo.png", "c:\\PHOTO.PNG" }, "C:\\Photo.png" };
    }

    [Theory]
    [MemberData(nameof(EquivalentBatchPaths))]
    public async Task ProcessPathsAsync_WhenDirectInputContainsEquivalentPaths_ShouldCountBatchDuplicate(
        string[] paths,
        string scannedPath)
    {
        _harness.Scanner
            .Setup(service => service.CreateFileItemAsync(scannedPath))
            .ReturnsAsync(new FileItem { Path = scannedPath });

        var result = await _harness.Service.ProcessPathsAsync(paths, maxItemCount: 10000, currentCount: 0);

        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(1, result.DuplicateCount);
        Assert.Equal(0, result.IgnoredCount);
        Assert.Single(result.NewItems);
        Assert.Equal(scannedPath, result.NewItems[0].Path);
        _harness.Scanner.Verify(service => service.CreateFileItemAsync(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ProcessPathsAsync_WhenExistingAndBatchDuplicatesExist_ShouldCountBoth()
    {
        string[] paths = ["C:\\existing.png", "C:\\new.png", "C:\\new.png"];
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "C:\\existing.png" };

        _harness.Scanner
            .Setup(service => service.CreateFileItemAsync("C:\\new.png"))
            .ReturnsAsync(new FileItem { Path = "C:\\new.png" });

        var result = await _harness.Service.ProcessPathsAsync(paths, maxItemCount: 10000, currentCount: 0, existing);

        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(2, result.DuplicateCount);
        Assert.Equal(0, result.IgnoredCount);
        Assert.Single(result.NewItems);
        Assert.Equal("C:\\new.png", result.NewItems[0].Path);
        _harness.Scanner.Verify(service => service.CreateFileItemAsync("C:\\new.png"), Times.Once);
    }

    [Fact]
    public async Task ProcessPathsAsync_WhenAlreadyAtLimit_ShouldReturnZeroSuccess()
    {
        string[] paths = ["C:\\test.png"];

        var result = await _harness.Service.ProcessPathsAsync(paths, maxItemCount: 100, currentCount: 100);

        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(1, result.IgnoredCount);
    }

    [Fact]
    public async Task ProcessPathsAsync_FolderScan_ShouldObeyLimit()
    {
        string folderPath = _harness.TempDirectory.EnsureDirectory("limit");
        var fakeFiles = new List<FileInfo>
        {
            new(Path.Combine(folderPath, "f1.png")),
            new(Path.Combine(folderPath, "f2.png")),
            new(Path.Combine(folderPath, "f3.png"))
        };

        _harness.Scanner
            .Setup(service => service.GetFilesInFolder(folderPath))
            .Returns(fakeFiles);
        _harness.Scanner
            .Setup(service => service.CreateFileItemAsync(It.IsAny<string>()))
            .ReturnsAsync((string path) => new FileItem { Path = path });

        var result = await _harness.Service.ProcessPathsAsync([folderPath], maxItemCount: 5, currentCount: 3);

        Assert.Equal(2, result.SuccessCount);
        Assert.Equal(1, result.IgnoredCount);
    }

    [Fact]
    public async Task ProcessPathsAsync_WhenDirectFileAndFolderOverlap_ShouldPreferDirectFileAndCountFolderDuplicate()
    {
        string folderPath = _harness.TempDirectory.EnsureDirectory("overlap");
        string directPath = Path.Combine(folderPath, "same.png");
        var fakeFiles = new List<FileInfo> { new(directPath) };

        _harness.Scanner
            .Setup(service => service.GetFilesInFolder(folderPath))
            .Returns(fakeFiles);
        _harness.Scanner
            .Setup(service => service.CreateFileItemAsync(directPath))
            .ReturnsAsync(new FileItem { Path = directPath });

        var result = await _harness.Service.ProcessPathsAsync([directPath, folderPath], maxItemCount: 10000, currentCount: 0);

        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(1, result.DuplicateCount);
        Assert.Equal(0, result.IgnoredCount);
        Assert.Single(result.NewItems);
        Assert.Equal(directPath, result.NewItems[0].Path);
        _harness.Scanner.Verify(service => service.CreateFileItemAsync(directPath), Times.Once);
    }

    [Fact]
    public async Task ProcessPathsAsync_WhenTwoFoldersReturnSameFile_ShouldAddOnceAndCountDuplicate()
    {
        string firstFolder = _harness.TempDirectory.EnsureDirectory("first");
        string secondFolder = _harness.TempDirectory.EnsureDirectory("second");
        string sharedPath = Path.Combine(firstFolder, "shared.png");
        var fakeFiles = new List<FileInfo> { new(sharedPath) };

        _harness.Scanner
            .Setup(service => service.GetFilesInFolder(firstFolder))
            .Returns(fakeFiles);
        _harness.Scanner
            .Setup(service => service.GetFilesInFolder(secondFolder))
            .Returns(fakeFiles);
        _harness.Scanner
            .Setup(service => service.CreateFileItemAsync(sharedPath))
            .ReturnsAsync(new FileItem { Path = sharedPath });

        var result = await _harness.Service.ProcessPathsAsync([firstFolder, secondFolder], maxItemCount: 10000, currentCount: 0);

        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(1, result.DuplicateCount);
        Assert.Equal(0, result.IgnoredCount);
        Assert.Single(result.NewItems);
        _harness.Scanner.Verify(service => service.CreateFileItemAsync(sharedPath), Times.Once);
    }

    [Fact]
    public async Task ProcessPathsAsync_WhenCapacityLimited_ShouldApplyLimitAfterBatchDuplicates()
    {
        string[] paths = ["C:\\a.png", "C:\\a.png", "C:\\b.png", "C:\\c.png"];

        _harness.Scanner
            .Setup(service => service.CreateFileItemAsync(It.IsAny<string>()))
            .ReturnsAsync((string path) => new FileItem { Path = path });

        var result = await _harness.Service.ProcessPathsAsync(paths, maxItemCount: 2, currentCount: 0);

        Assert.Equal(2, result.SuccessCount);
        Assert.Equal(1, result.DuplicateCount);
        Assert.Equal(1, result.IgnoredCount);
        Assert.Equal(["C:\\a.png", "C:\\b.png"], result.NewItems.Select(item => item.Path).ToArray());
        _harness.Scanner.Verify(service => service.CreateFileItemAsync(It.IsAny<string>()), Times.Exactly(2));
    }

    public void Dispose()
    {
        _harness.Dispose();
    }
}
