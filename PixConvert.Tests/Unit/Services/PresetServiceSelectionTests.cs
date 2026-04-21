using Microsoft.Extensions.Logging;
using Moq;
using PixConvert.Models;
using PixConvert.Services;
using Xunit;

namespace PixConvert.Tests;

public class PresetServiceSelectionTests
{
    private readonly PresetService _presetService;

    public PresetServiceSelectionTests()
    {
        var loggerMock = new Mock<ILogger<PresetService>>();
        _presetService = new PresetService(loggerMock.Object, new FakeLanguageService());
    }

    [Fact]
    public void ValidPresetData_WhenAnimationTargetFormatIsNull_ShouldReturnTrue()
    {
        var settings = new ConvertSettings
        {
            StandardTargetFormat = "JPEG",
            AnimationTargetFormat = null,
            StandardBackgroundColor = "#FFFFFF"
        };

        var result = _presetService.ValidPresetData(settings, out string errorKey);

        Assert.True(result);
        Assert.True(string.IsNullOrEmpty(errorKey));
    }

    [Fact]
    public void ConvertSettings_DefaultJpegSubsampling_ShouldBe444()
    {
        var settings = new ConvertSettings();

        Assert.Equal(JpegChromaSubsamplingMode.Chroma444, settings.StandardJpegChromaSubsampling);
    }
}
