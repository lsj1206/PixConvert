using Microsoft.Extensions.Logging;
using Moq;
using PixConvert.Models;
using PixConvert.Services;
using Xunit;

namespace PixConvert.Tests;

public class PresetServiceValidationTests
{
    private readonly PresetService _presetService;

    public PresetServiceValidationTests()
    {
        var loggerMock = new Mock<ILogger<PresetService>>();
        _presetService = new PresetService(loggerMock.Object, new FakeLanguageService());
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(101, false)]
    [InlineData(50, true)]
    public void ValidPresetData_ShouldValidateStandardQuality(int quality, bool expected)
    {
        var settings = new ConvertSettings
        {
            StandardQuality = quality,
            StandardTargetFormat = "JPEG",
            AnimationTargetFormat = "GIF",
            StandardBackgroundColor = "#FFFFFF"
        };

        AssertValidationResult(settings, expected);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(101, false)]
    [InlineData(50, true)]
    public void ValidPresetData_ShouldValidateAnimationQuality(int quality, bool expected)
    {
        var settings = new ConvertSettings
        {
            AnimationQuality = quality,
            StandardTargetFormat = "JPEG",
            AnimationTargetFormat = "GIF",
            StandardBackgroundColor = "#FFFFFF"
        };

        AssertValidationResult(settings, expected);
    }

    [Theory]
    [InlineData("INVALID", false)]
    [InlineData("JPEG", true)]
    [InlineData("Avif", true)]
    [InlineData("", false)]
    public void ValidPresetData_ShouldValidateStandardTargetFormat(string format, bool expected)
    {
        var settings = new ConvertSettings
        {
            StandardTargetFormat = format,
            AnimationTargetFormat = "GIF",
            StandardBackgroundColor = "#FFFFFF"
        };

        var result = _presetService.ValidPresetData(settings, out _);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ValidPresetData_WhenSettingsIsNull_ShouldReturnFalse()
    {
        var result = _presetService.ValidPresetData(null!, out string errorKey);

        Assert.False(result);
        Assert.Equal("Msg_Error_ConfigInvalid", errorKey);
    }

    [Fact]
    public void ValidPresetData_WhenStandardBackgroundHexIsInvalid_ShouldReturnFalse()
    {
        var settings = new ConvertSettings
        {
            StandardTargetFormat = "JPEG",
            AnimationTargetFormat = "GIF",
            StandardBackgroundColor = "INVALID"
        };

        AssertValidationResult(settings, expected: false);
    }

    [Fact]
    public void ValidPresetData_WhenCustomPathIsEmpty_ShouldReturnFalse()
    {
        var settings = new ConvertSettings
        {
            StandardTargetFormat = "JPEG",
            AnimationTargetFormat = "GIF",
            SaveLocation = SaveLocationType.Custom,
            CustomOutputPath = "",
            StandardBackgroundColor = "#FFFFFF"
        };

        AssertValidationResult(settings, expected: false);
    }

    [Fact]
    public void ValidPresetData_WhenSubFolderNameIsEmpty_ShouldReturnFalse()
    {
        var settings = new ConvertSettings
        {
            StandardTargetFormat = "JPEG",
            AnimationTargetFormat = "GIF",
            FolderMethod = SaveFolderMethod.CreateFolder,
            OutputSubFolderName = "",
            StandardBackgroundColor = "#FFFFFF"
        };

        var result = _presetService.ValidPresetData(settings, out _);

        Assert.False(result);
    }

    [Fact]
    public void ValidPresetData_WhenSubFolderNameHasInvalidChars_ShouldReturnFalse()
    {
        var settings = new ConvertSettings
        {
            StandardTargetFormat = "JPEG",
            AnimationTargetFormat = "GIF",
            FolderMethod = SaveFolderMethod.CreateFolder,
            OutputSubFolderName = "Invalid/Folder?Name",
            StandardBackgroundColor = "#FFFFFF"
        };

        var result = _presetService.ValidPresetData(settings, out _);

        Assert.False(result);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(6, true)]
    [InlineData(9, true)]
    [InlineData(10, false)]
    public void ValidPresetData_ShouldValidateStandardPngCompressionLevel(int compressionLevel, bool expected)
    {
        var settings = new ConvertSettings
        {
            StandardTargetFormat = "PNG",
            AnimationTargetFormat = "GIF",
            StandardBackgroundColor = "#FFFFFF",
            StandardPngCompressionLevel = compressionLevel
        };

        AssertValidationResult(settings, expected);
    }

    [Fact]
    public void ValidPresetData_WhenJpegSubsamplingEnumIsInvalid_ShouldReturnFalse()
    {
        var settings = new ConvertSettings
        {
            StandardTargetFormat = "JPEG",
            AnimationTargetFormat = "GIF",
            StandardBackgroundColor = "#FFFFFF",
            StandardJpegChromaSubsampling = (JpegChromaSubsamplingMode)999
        };

        AssertValidationResult(settings, expected: false);
    }

    [Fact]
    public void ValidPresetData_WhenAvifOptionsAreValid_ShouldReturnTrue()
    {
        var settings = new ConvertSettings
        {
            StandardTargetFormat = "AVIF",
            AnimationTargetFormat = "GIF",
            StandardBackgroundColor = "#FFFFFF",
            StandardAvifChromaSubsampling = AvifChromaSubsamplingMode.Off,
            StandardAvifEncodingEffort = 9,
            StandardAvifBitDepth = AvifBitDepthMode.Bit10
        };

        AssertValidationResult(settings, expected: true);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(4, true)]
    [InlineData(9, true)]
    [InlineData(10, false)]
    public void ValidPresetData_ShouldValidateStandardAvifEncodingEffort(int effort, bool expected)
    {
        var settings = new ConvertSettings
        {
            StandardTargetFormat = "AVIF",
            AnimationTargetFormat = "GIF",
            StandardBackgroundColor = "#FFFFFF",
            StandardAvifEncodingEffort = effort
        };

        AssertValidationResult(settings, expected);
    }

    [Theory]
    [InlineData(-0.1, false)]
    [InlineData(0.0, true)]
    [InlineData(16.0, true)]
    [InlineData(32.0, true)]
    [InlineData(32.1, false)]
    public void ValidPresetData_ShouldValidateAnimationGifInterframeMaxError(double value, bool expected)
    {
        var settings = new ConvertSettings
        {
            StandardTargetFormat = "JPEG",
            AnimationTargetFormat = "GIF",
            StandardBackgroundColor = "#FFFFFF",
            AnimationGifInterframeMaxError = value
        };

        AssertValidationResult(settings, expected);
    }

    [Theory]
    [InlineData(-0.1, false)]
    [InlineData(0.0, true)]
    [InlineData(16.0, true)]
    [InlineData(32.0, true)]
    [InlineData(32.1, false)]
    public void ValidPresetData_ShouldValidateAnimationGifInterpaletteMaxError(double value, bool expected)
    {
        var settings = new ConvertSettings
        {
            StandardTargetFormat = "JPEG",
            AnimationTargetFormat = "GIF",
            StandardBackgroundColor = "#FFFFFF",
            AnimationGifInterpaletteMaxError = value
        };

        AssertValidationResult(settings, expected);
    }

    [Fact]
    public void ValidPresetData_WhenGifPalettePresetEnumIsInvalid_ShouldReturnFalse()
    {
        var settings = new ConvertSettings
        {
            StandardTargetFormat = "JPEG",
            AnimationTargetFormat = "GIF",
            StandardBackgroundColor = "#FFFFFF",
            AnimationGifPalettePreset = (GifPalettePreset)999
        };

        AssertValidationResult(settings, expected: false);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(6, true)]
    [InlineData(9, true)]
    [InlineData(10, false)]
    public void ValidPresetData_ShouldValidateAnimationGifEncodingEffort(int effort, bool expected)
    {
        var settings = new ConvertSettings
        {
            StandardTargetFormat = "JPEG",
            AnimationTargetFormat = "GIF",
            StandardBackgroundColor = "#FFFFFF",
            AnimationGifEncodingEffort = effort
        };

        AssertValidationResult(settings, expected);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(4, true)]
    [InlineData(6, true)]
    [InlineData(7, false)]
    public void ValidPresetData_ShouldValidateAnimationWebpEncodingEffort(int effort, bool expected)
    {
        var settings = new ConvertSettings
        {
            StandardTargetFormat = "JPEG",
            AnimationTargetFormat = "WEBP",
            StandardBackgroundColor = "#FFFFFF",
            AnimationWebpEncodingEffort = effort
        };

        AssertValidationResult(settings, expected);
    }

    [Fact]
    public void ValidPresetData_WhenWebpPresetEnumIsInvalid_ShouldReturnFalse()
    {
        var settings = new ConvertSettings
        {
            StandardTargetFormat = "JPEG",
            AnimationTargetFormat = "WEBP",
            StandardBackgroundColor = "#FFFFFF",
            AnimationWebpPreset = (WebpPresetMode)999
        };

        AssertValidationResult(settings, expected: false);
    }

    private void AssertValidationResult(ConvertSettings settings, bool expected)
    {
        var result = _presetService.ValidPresetData(settings, out string errorKey);

        Assert.Equal(expected, result);
        if (!expected)
            Assert.Equal("Msg_Error_ConfigInvalid", errorKey);
        else
            Assert.True(string.IsNullOrEmpty(errorKey));
    }
}
