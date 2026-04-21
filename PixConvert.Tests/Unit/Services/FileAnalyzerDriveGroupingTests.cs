using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using PixConvert.Models;
using Xunit;

namespace PixConvert.Tests;

public sealed class FileAnalyzerDriveGroupingTests : IDisposable
{
    private readonly FileAnalyzerServiceTestHarness _harness = new();

    [Fact]
    public async Task ProcessPathsAsync_MixedDrives_ShouldQueryParallelismByFirstSeenDriveOrder()
    {
        string[] paths =
        [
            @"D:\d1.png",
            @"C:\c1.png",
            @"D:\d2.png",
            @"E:\e1.png"
        ];
        var queriedPaths = new List<string>();

        _harness.DriveInfo
            .Setup(service => service.GetOptimalParallelismAsync(It.IsAny<string>()))
            .Callback<string>(path => queriedPaths.Add(path))
            .ReturnsAsync(1);
        _harness.Scanner
            .Setup(service => service.CreateFileItemAsync(It.IsAny<string>()))
            .ReturnsAsync((string path) => new FileItem { Path = path });

        var result = await _harness.Service.ProcessPathsAsync(paths, maxItemCount: 100, currentCount: 0);

        Assert.Equal([@"D:\d1.png", @"C:\c1.png", @"E:\e1.png"], queriedPaths);
        Assert.Equal(4, result.SuccessCount);
        Assert.Equal(paths, result.NewItems.Select(item => item.Path).ToArray());
    }

    [Fact]
    public async Task ProcessPathsAsync_SingleDrive_ShouldQueryParallelismOnlyOnce()
    {
        string[] paths =
        [
            @"C:\a.png",
            @"C:\b.png",
            @"C:\c.png"
        ];
        int queryCount = 0;
        string? firstQueryPath = null;

        _harness.DriveInfo
            .Setup(service => service.GetOptimalParallelismAsync(It.IsAny<string>()))
            .Callback<string>(path =>
            {
                queryCount++;
                firstQueryPath ??= path;
            })
            .ReturnsAsync(1);
        _harness.Scanner
            .Setup(service => service.CreateFileItemAsync(It.IsAny<string>()))
            .ReturnsAsync((string path) => new FileItem { Path = path });

        var result = await _harness.Service.ProcessPathsAsync(paths, maxItemCount: 100, currentCount: 0);

        Assert.Equal(1, queryCount);
        Assert.Equal(@"C:\a.png", firstQueryPath);
        Assert.Equal(3, result.SuccessCount);
    }

    public void Dispose()
    {
        _harness.Dispose();
    }
}
