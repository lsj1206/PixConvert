using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using PixConvert.Models;
using PixConvert.Services.Providers;

namespace PixConvert.Tests;

public class ConversionSessionTests : IDisposable
{
    private readonly string _tempDir;

    public ConversionSessionTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "PixConvert.Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void OverwritePolicy_SameAsOriginal_ShouldChangeToSuffix()
    {
        // Arrange
        using var session = new ConversionSession();
        string originalFile = Path.Combine(_tempDir, "Original.jpg");
        string basePath = originalFile;
        File.WriteAllText(originalFile, "dummy"); // 원본 파일 존재

        // Act
        var (path, collision) = OutputPathResolver.ApplyOverwritePolicy(
            basePath, OverwritePolicy.Overwrite, session, originalFile);

        // Assert
        Assert.False(collision);
        Assert.NotNull(path);
        Assert.Contains("Original_1.jpg", path);
    }

    [Fact]
    public void SkipPolicy_ShouldReturnNull_WhenPathAlreadyExists()
    {
        // Arrange
        using var session = new ConversionSession();
        string existingFile = Path.Combine(_tempDir, "SkipFile.jpg");
        File.WriteAllText(existingFile, "dummy");

        // Act
        var (path, collision) = OutputPathResolver.ApplyOverwritePolicy(
            existingFile, OverwritePolicy.Skip, session, "dummy.original");

        // Assert
        Assert.False(collision);
        Assert.Null(path);
    }

    [Fact]
    public void ParallelSuffix_ShouldAssignUniquePaths()
    {
        // Arrange
        using var session = new ConversionSession();
        string basePath = Path.Combine(_tempDir, "ParallelSuffix.png");

        // Act
        var paths = new string[100];
        Parallel.For(0, 100, i =>
        {
            var (path, _) = OutputPathResolver.ApplyOverwritePolicy(
                basePath, OverwritePolicy.Suffix, session, $"dummy{i}.original");
            paths[i] = path!;
        });

        // Assert
        var uniquePaths = new System.Collections.Generic.HashSet<string>(paths);
        Assert.Equal(100, uniquePaths.Count);

        foreach (var p in uniquePaths)
        {
            Assert.NotNull(p);
            Assert.StartsWith(Path.Combine(_tempDir, "ParallelSuffix"), p);
        }
    }

    [Fact]
    public void OverwritePolicy_ParallelRequests_ShouldAssignUniquePathsAndLogCollision()
    {
        // Arrange
        using var session = new ConversionSession();
        string basePath = Path.Combine(_tempDir, "CollisionTest.png");

        // Act
        int collisionCount = 0;
        var paths = new string[100];
        Parallel.For(0, 100, i =>
        {
            var (path, isCollision) = OutputPathResolver.ApplyOverwritePolicy(
                basePath, OverwritePolicy.Overwrite, session, $"dummy{i}.original");

            if (isCollision)
            {
                System.Threading.Interlocked.Increment(ref collisionCount);
            }
            paths[i] = path!;
        });

        // Assert
        var uniquePaths = new System.Collections.Generic.HashSet<string>(paths);
        Assert.Equal(100, uniquePaths.Count);
        Assert.Contains(basePath, uniquePaths);
        Assert.Equal(99, collisionCount);
        foreach (var p in uniquePaths)
        {
            Assert.StartsWith(Path.Combine(_tempDir, "CollisionTest"), p);
        }
    }

    [Fact]
    public void OverwritePolicy_SequentialRequests_ShouldUseBaseThenSuffixes()
    {
        using var session = new ConversionSession();
        string basePath = Path.Combine(_tempDir, "OverwriteSequential.png");

        var first = OutputPathResolver.ApplyOverwritePolicy(
            basePath, OverwritePolicy.Overwrite, session, "dummy1.original");
        var second = OutputPathResolver.ApplyOverwritePolicy(
            basePath, OverwritePolicy.Overwrite, session, "dummy2.original");
        var third = OutputPathResolver.ApplyOverwritePolicy(
            basePath, OverwritePolicy.Overwrite, session, "dummy3.original");

        Assert.Equal(basePath, first.Path);
        Assert.False(first.IsCollision);
        Assert.Equal(Path.Combine(_tempDir, "OverwriteSequential_1.png"), second.Path);
        Assert.True(second.IsCollision);
        Assert.Equal(Path.Combine(_tempDir, "OverwriteSequential_2.png"), third.Path);
        Assert.True(third.IsCollision);
    }

    [Fact]
    public void OverwritePolicy_CollisionSuffix_WhenSuffixExistsOnDisk_ShouldUseNextAvailable()
    {
        using var session = new ConversionSession();
        string basePath = Path.Combine(_tempDir, "OverwriteExistingSuffix.jpg");
        string existingSuffix = Path.Combine(_tempDir, "OverwriteExistingSuffix_1.jpg");
        File.WriteAllText(existingSuffix, "exists");

        var first = OutputPathResolver.ApplyOverwritePolicy(
            basePath, OverwritePolicy.Overwrite, session, "dummy1.original");
        var second = OutputPathResolver.ApplyOverwritePolicy(
            basePath, OverwritePolicy.Overwrite, session, "dummy2.original");

        Assert.Equal(basePath, first.Path);
        Assert.False(first.IsCollision);
        Assert.Equal(Path.Combine(_tempDir, "OverwriteExistingSuffix_2.jpg"), second.Path);
        Assert.True(second.IsCollision);
    }

    // ── 직접 테스트 (Direct Tests) ──────────────────────────────────────────

    [Fact]
    public void TryReserve_WhenPathFree_ShouldSucceed()
    {
        using var session = new ConversionSession();
        string path = Path.Combine(_tempDir, "Free.jpg");

        bool result = session.TryReserve(path);

        Assert.True(result);
    }

    [Fact]
    public void TryReserve_WhenAlreadyReserved_ShouldFail()
    {
        using var session = new ConversionSession();
        string path = Path.Combine(_tempDir, "Reserved.jpg");
        session.TryReserve(path);

        bool result = session.TryReserve(path);

        Assert.False(result);
    }

    [Fact]
    public void TryReserve_WhenFileExistsOnDisk_ShouldFail()
    {
        using var session = new ConversionSession();
        string path = Path.Combine(_tempDir, "OnDisk.jpg");
        File.WriteAllText(path, "exists");

        bool result = session.TryReserve(path);

        Assert.False(result);
    }

    [Fact]
    public void TryReserve_DifferentSessions_ShouldNotShareState()
    {
        // 핵심: 세션 간 데이터 오염(Static Leak)이 없는지 확인
        using var session1 = new ConversionSession();
        using var session2 = new ConversionSession();
        string path = Path.Combine(_tempDir, "Isolated.jpg");

        session1.TryReserve(path);
        bool result2 = session2.TryReserve(path); // session1과는 별개이므로 디스크에만 없으면 성공해야 함

        Assert.True(result2);
    }

    [Fact]
    public void FindAndReserveSuffixed_WhenBasePathFree_ShouldReturnBasePath()
    {
        using var session = new ConversionSession();
        string path = Path.Combine(_tempDir, "FindFree.jpg");

        string result = session.FindAndReserveSuffixed(path);

        Assert.Equal(path, result);
    }

    [Fact]
    public void FindAndReserveSuffixed_WhenBasePathReserved_ShouldReturnSuffixed()
    {
        using var session = new ConversionSession();
        string path = Path.Combine(_tempDir, "FindReserved.jpg");
        session.TryReserve(path);

        string result = session.FindAndReserveSuffixed(path);

        Assert.Contains("_1.jpg", result);
    }

    [Fact]
    public void FindAndReserveSuffixed_WhenBasePathExistsOnDisk_ShouldReturnSuffixed()
    {
        using var session = new ConversionSession();
        string path = Path.Combine(_tempDir, "FindOnDisk.jpg");
        File.WriteAllText(path, "exists");

        string result = session.FindAndReserveSuffixed(path);

        Assert.Contains("_1.jpg", result);
    }

    [Fact]
    public void ReserveOverwrite_FirstCall_ShouldNotCollide()
    {
        using var session = new ConversionSession();
        string path = Path.Combine(_tempDir, "ForceFirst.jpg");

        var (reservedPath, isCollision) = session.ReserveOverwrite(path);

        Assert.Equal(path, reservedPath);
        Assert.False(isCollision);
    }

    [Fact]
    public void ReserveOverwrite_SecondCallSamePath_ShouldCollideAndUseSuffix()
    {
        using var session = new ConversionSession();
        string path = Path.Combine(_tempDir, "ForceSecond.jpg");
        session.ReserveOverwrite(path);

        var (reservedPath, isCollision) = session.ReserveOverwrite(path);

        Assert.Equal(Path.Combine(_tempDir, "ForceSecond_1.jpg"), reservedPath);
        Assert.True(isCollision);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }
    }
}
