using System;
using System.IO;
using Xunit;
using PixConvert.Models;
using PixConvert.Services.Providers;

namespace PixConvert.Tests;

public class OutputPathResolverTests
{
    private readonly FileItem _testFile;

    public OutputPathResolverTests()
    {
        _testFile = new FileItem
        {
            Path = @"C:\Photos\Image.png",
            FileSignature = "PNG",
            IsAnimation = false
        };
    }

    [Fact]
    public void Resolve_SameFolder_NoFolder_ShouldReturnOriginalDirectory()
    {
        // Arrange
        var settings = new ConvertSettings
        {
            OutputLocation = OutputLocationType.SameAsOriginal,
            FolderStrategy = OutputFolderStrategy.NoFolder,
            StandardTargetFormat = "JPEG"
        };

        // Act
        var result = OutputPathResolver.Resolve(_testFile, settings);

        // Assert
        Assert.Equal(@"C:\Photos\Image.jpg", result);
    }

    [Fact]
    public void Resolve_SameFolder_WithFolder_ShouldReturnSubFolder()
    {
        // Arrange
        var settings = new ConvertSettings
        {
            OutputLocation = OutputLocationType.SameAsOriginal,
            FolderStrategy = OutputFolderStrategy.CreateFolder,
            OutputSubFolderName = "Converted",
            StandardTargetFormat = "PNG"
        };

        // Act
        var result = OutputPathResolver.Resolve(_testFile, settings);

        // Assert
        Assert.Equal(@"C:\Photos\Converted\Image.png", result);
    }

    [Fact]
    public void Resolve_CustomPath_NoFolder_ShouldReturnCustomFolder()
    {
        // Arrange
        var settings = new ConvertSettings
        {
            OutputLocation = OutputLocationType.Custom,
            CustomOutputPath = @"D:\Output",
            FolderStrategy = OutputFolderStrategy.NoFolder,
            StandardTargetFormat = "WEBP"
        };

        // Act
        var result = OutputPathResolver.Resolve(_testFile, settings);

        // Assert
        Assert.Equal(@"D:\Output\Image.webp", result);
    }

    [Fact]
    public void Resolve_CustomPath_WithFolder_ShouldReturnCustomSubFolder()
    {
        // Arrange
        var settings = new ConvertSettings
        {
            OutputLocation = OutputLocationType.Custom,
            CustomOutputPath = @"D:\Output",
            FolderStrategy = OutputFolderStrategy.CreateFolder,
            OutputSubFolderName = "MyPix",
            StandardTargetFormat = "AVIF"
        };

        // Act
        var result = OutputPathResolver.Resolve(_testFile, settings);

        // Assert
        Assert.Equal(@"D:\Output\MyPix\Image.avif", result);
    }

    [Fact]
    public void Resolve_WhenCustomPathEmpty_ShouldFallbackToOriginalDirectory()
    {
        // Arrange
        var settings = new ConvertSettings
        {
            OutputLocation = OutputLocationType.Custom,
            CustomOutputPath = "", // Empty
            FolderStrategy = OutputFolderStrategy.NoFolder,
            StandardTargetFormat = "JPEG"
        };

        // Act
        var result = OutputPathResolver.Resolve(_testFile, settings);

        // Assert
        Assert.Equal(@"C:\Photos\Image.jpg", result);
    }

    [Fact]
    public void Resolve_WithLiteralFolderName_ShouldNotReplaceTokens()
    {
        // Arrange - v3에서는 토큰 치환을 하지 않으므로 {yyyy}가 그대로 나와야 함
        var settings = new ConvertSettings
        {
            OutputLocation = OutputLocationType.SameAsOriginal,
            FolderStrategy = OutputFolderStrategy.CreateFolder,
            OutputSubFolderName = "Pix_{yyyy}",
            StandardTargetFormat = "JPEG"
        };

        // Act
        var result = OutputPathResolver.Resolve(_testFile, settings);

        // Assert
        Assert.Equal(@"C:\Photos\Pix_{yyyy}\Image.jpg", result);
    }
}
