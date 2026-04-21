using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using PixConvert.Models;
using Xunit;

namespace PixConvert.Tests;

public sealed class FileAnalyzerPathProcessingTests : IDisposable
{
    private readonly FileAnalyzerServiceTestHarness _harness = new();

    [Fact]
    public async Task ProcessPathsAsync_WhenUnderLimit_ShouldAddAllFiles()
    {
        string[] paths = ["C:\\test1.png", "C:\\test2.png"];

        _harness.Scanner
            .Setup(service => service.CreateFileItemAsync(It.IsAny<string>()))
            .ReturnsAsync((string path) => new FileItem { Path = path });

        var result = await _harness.Service.ProcessPathsAsync(paths, maxItemCount: 10000, currentCount: 0);

        Assert.Equal(2, result.SuccessCount);
        Assert.Equal(0, result.IgnoredCount);
        Assert.Equal(0, result.DuplicateCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal(2, result.NewItems.Count);
    }

    [Fact]
    public async Task ProcessPathsAsync_WhenSomeFileItemsFail_ShouldCountFailuresAndKeepSuccessfulItems()
    {
        string[] paths = ["C:\\ok.png", "C:\\bad.png", "C:\\ok2.png"];

        _harness.Scanner
            .Setup(service => service.CreateFileItemAsync(It.IsAny<string>()))
            .Returns((string path) => Task.FromResult<FileItem?>(
                path.Contains("bad", StringComparison.OrdinalIgnoreCase)
                    ? null
                    : new FileItem { Path = path }));

        var result = await _harness.Service.ProcessPathsAsync(paths, maxItemCount: 10000, currentCount: 0);

        Assert.Equal(2, result.SuccessCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Equal(0, result.IgnoredCount);
        Assert.Equal(0, result.DuplicateCount);
        Assert.Equal(["C:\\ok.png", "C:\\ok2.png"], result.NewItems.Select(item => item.Path).ToArray());
    }

    [Fact]
    public async Task ProcessPathsAsync_ShouldLogSummaryWithFailedCount()
    {
        string[] paths = ["C:\\ok.png", "C:\\bad.png"];

        _harness.Scanner
            .Setup(service => service.CreateFileItemAsync(It.IsAny<string>()))
            .Returns((string path) => Task.FromResult<FileItem?>(
                path.Contains("bad", StringComparison.OrdinalIgnoreCase)
                    ? null
                    : new FileItem { Path = path }));

        await _harness.Service.ProcessPathsAsync(paths, maxItemCount: 10000, currentCount: 0);

        _harness.Logger.Verify(
            logger => logger.Log(
                It.Is<LogLevel>(level => level == LogLevel.Information),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((value, _) =>
                    value.ToString()!.Contains("InputPaths=2", StringComparison.Ordinal) &&
                    value.ToString()!.Contains("Added=1", StringComparison.Ordinal) &&
                    value.ToString()!.Contains("Duplicate=0", StringComparison.Ordinal) &&
                    value.ToString()!.Contains("Ignored=0", StringComparison.Ordinal) &&
                    value.ToString()!.Contains("Failed=1", StringComparison.Ordinal) &&
                    value.ToString()!.Contains("ElapsedMs=", StringComparison.Ordinal)),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessPathsAsync_WhenAllFileItemsFail_ShouldCountFailures()
    {
        string[] paths = ["C:\\bad1.png", "C:\\bad2.png"];

        _harness.Scanner
            .Setup(service => service.CreateFileItemAsync(It.IsAny<string>()))
            .ReturnsAsync((FileItem?)null);

        var result = await _harness.Service.ProcessPathsAsync(paths, maxItemCount: 10000, currentCount: 0);

        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(2, result.FailedCount);
        Assert.Empty(result.NewItems);
    }

    [Fact]
    public async Task ProcessPathsAsync_WhenFolderIsEmpty_ShouldReturnZeroCounts()
    {
        string folderPath = _harness.TempDirectory.EnsureDirectory("empty");

        _harness.Scanner
            .Setup(service => service.GetFilesInFolder(folderPath))
            .Returns([]);

        var result = await _harness.Service.ProcessPathsAsync([folderPath], maxItemCount: 10000, currentCount: 0);

        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal(0, result.IgnoredCount);
        Assert.Equal(0, result.DuplicateCount);
    }

    public void Dispose()
    {
        _harness.Dispose();
    }
}
