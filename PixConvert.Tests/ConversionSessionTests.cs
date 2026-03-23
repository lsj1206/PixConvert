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

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }
    }
}
