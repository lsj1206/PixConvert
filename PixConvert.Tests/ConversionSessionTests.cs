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
    public void OverwritePolicy_ParallelRequests_ShouldLogCollision()
    {
        // Arrange
        using var session = new ConversionSession();
        string basePath = Path.Combine(_tempDir, "CollisionTest.png");

        // Act
        int collisionCount = 0;
        Parallel.For(0, 100, i =>
        {
            var (path, isCollision) = OutputPathResolver.ApplyOverwritePolicy(
                basePath, OverwritePolicy.Overwrite, session, $"dummy{i}.original");
            
            if (isCollision)
            {
                System.Threading.Interlocked.Increment(ref collisionCount);
            }
            Assert.Equal(basePath, path);
        });

        // Assert
        // 첫 번째 예약은 충돌이 아니고 그 후 99번은 모두 충돌
        Assert.Equal(99, collisionCount);
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
    public void ReserveForce_FirstCall_ShouldNotCollide()
    {
        using var session = new ConversionSession();
        string path = Path.Combine(_tempDir, "ForceFirst.jpg");

        var (reservedPath, isCollision) = session.ReserveForce(path);

        Assert.Equal(path, reservedPath);
        Assert.False(isCollision);
    }

    [Fact]
    public void ReserveForce_SecondCallSamePath_ShouldCollide()
    {
        using var session = new ConversionSession();
        string path = Path.Combine(_tempDir, "ForceSecond.jpg");
        session.ReserveForce(path);

        var (reservedPath, isCollision) = session.ReserveForce(path);

        Assert.Equal(path, reservedPath);
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
