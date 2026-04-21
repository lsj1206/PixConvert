using System.IO;
using Microsoft.Extensions.Logging;
using Moq;
using PixConvert.Models;
using PixConvert.Services.Providers;
using Xunit;

namespace PixConvert.Tests;

public sealed class ProviderConversionHelperTests : IDisposable
{
    private readonly TempDirectoryFixture _tempDirectory = new("ProviderHelperTests_");

    [Fact]
    public void PrepareOutputPath_WhenOutputDirectoryDoesNotExist_ShouldCreateDirectory()
    {
        string inputPath = _tempDirectory.CreatePath("input.png");
        string outputRoot = _tempDirectory.CreatePath("export");
        var file = new FileItem { Path = inputPath, FileSignature = "PNG" };
        var settings = new ConvertSettings
        {
            StandardTargetFormat = "JPEG",
            SaveLocation = SaveLocationType.Custom,
            CustomOutputPath = outputRoot,
            FolderMethod = SaveFolderMethod.CreateFolder,
            OutputSubFolderName = "Converted",
            OverwritePolicy = OverwritePolicy.Overwrite
        };
        using var session = new ConversionSession();

        string? outputPath = ProviderConversionHelper.PrepareOutputPath(
            file,
            settings,
            session,
            Mock.Of<ILogger>(),
            new FakeLanguageService());

        Assert.Equal(Path.Combine(outputRoot, "Converted", "input.jpg"), outputPath);
        Assert.True(Directory.Exists(Path.Combine(outputRoot, "Converted")));
    }

    [Fact]
    public void PrepareOutputPath_WhenSkipPolicyCollides_ShouldReturnNull()
    {
        string inputPath = _tempDirectory.CreatePath("input.png");
        string existingOutput = _tempDirectory.CreatePath("input.jpg");
        File.WriteAllText(existingOutput, "existing");

        var file = new FileItem { Path = inputPath, FileSignature = "PNG" };
        var settings = new ConvertSettings
        {
            StandardTargetFormat = "JPEG",
            SaveLocation = SaveLocationType.SameAsOriginal,
            FolderMethod = SaveFolderMethod.NoFolder,
            OverwritePolicy = OverwritePolicy.Skip
        };
        using var session = new ConversionSession();

        string? outputPath = ProviderConversionHelper.PrepareOutputPath(
            file,
            settings,
            session,
            Mock.Of<ILogger>(),
            new FakeLanguageService());

        Assert.Null(outputPath);
    }

    [Fact]
    public void PrepareOutputPath_WhenOverwriteWouldReplaceOriginal_ShouldUseSuffixedPath()
    {
        string inputPath = _tempDirectory.CreatePath("image.png");
        File.WriteAllText(inputPath, "source");
        var file = new FileItem { Path = inputPath, FileSignature = "PNG" };
        var settings = new ConvertSettings
        {
            StandardTargetFormat = "PNG",
            SaveLocation = SaveLocationType.SameAsOriginal,
            FolderMethod = SaveFolderMethod.NoFolder,
            OverwritePolicy = OverwritePolicy.Overwrite
        };
        using var session = new ConversionSession();

        string? outputPath = ProviderConversionHelper.PrepareOutputPath(
            file,
            settings,
            session,
            Mock.Of<ILogger>(),
            new FakeLanguageService());

        Assert.Equal(_tempDirectory.CreatePath("image_1.png"), outputPath);
        Assert.NotEqual(inputPath, outputPath);
    }

    [Theory]
    [InlineData(false, "JPEG")]
    [InlineData(true, "GIF")]
    public void ResolveTargetFormat_ShouldReturnModeSpecificFormat(bool isAnimation, string expected)
    {
        var file = new FileItem { Path = @"C:\input.png", FileSignature = "PNG", IsAnimation = isAnimation };
        var settings = new ConvertSettings
        {
            StandardTargetFormat = "JPEG",
            AnimationTargetFormat = "GIF"
        };

        string format = ProviderConversionHelper.ResolveTargetFormat(file, settings);

        Assert.Equal(expected, format);
    }

    [Fact]
    public void GetQualityAndLossless_ShouldUseModeSpecificSettings()
    {
        var settings = new ConvertSettings
        {
            StandardQuality = 82,
            StandardLossless = false,
            AnimationQuality = 61,
            AnimationLossless = true
        };

        Assert.Equal(82, ProviderConversionHelper.GetQuality(settings, false));
        Assert.False(ProviderConversionHelper.GetLossless(settings, false));
        Assert.Equal(61, ProviderConversionHelper.GetQuality(settings, true));
        Assert.True(ProviderConversionHelper.GetLossless(settings, true));
    }

    [Fact]
    public void GetBackgroundColor_WhenRgbHexProvided_ShouldReturnOpaqueColor()
    {
        var settings = new ConvertSettings { StandardBackgroundColor = "#112233" };

        ProviderBackgroundColor color = ProviderConversionHelper.GetBackgroundColor(settings);

        Assert.Equal(new ProviderBackgroundColor(255, 0x11, 0x22, 0x33), color);
    }

    [Fact]
    public void GetBackgroundColor_WhenArgbHexProvided_ShouldRespectAlpha()
    {
        var settings = new ConvertSettings { StandardBackgroundColor = "#80112233" };

        ProviderBackgroundColor color = ProviderConversionHelper.GetBackgroundColor(settings);

        Assert.Equal(new ProviderBackgroundColor(0x80, 0x11, 0x22, 0x33), color);
    }

    [Fact]
    public void GetBackgroundColor_WhenHexIsInvalid_ShouldFallbackToWhite()
    {
        var settings = new ConvertSettings { StandardBackgroundColor = "#invalid" };

        ProviderBackgroundColor color = ProviderConversionHelper.GetBackgroundColor(settings);

        Assert.Equal(ProviderBackgroundColor.White, color);
    }

    public void Dispose()
    {
        _tempDirectory.Dispose();
    }
}
