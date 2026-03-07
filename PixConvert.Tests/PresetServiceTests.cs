using Microsoft.Extensions.Logging;
using Moq;
using PixConvert.Models;
using PixConvert.Services;
using System.IO;

namespace PixConvert.Tests;

public class PresetServiceTests
{
    private readonly Mock<ILogger<PresetService>> _loggerMock;
    private readonly Mock<ILanguageService> _languageMock;
    private readonly PresetService _presetService;

    public PresetServiceTests()
    {
        _loggerMock = new Mock<ILogger<PresetService>>();
        _languageMock = new Mock<ILanguageService>();
        _languageMock.Setup(x => x.GetString(It.IsAny<string>())).Returns((string key) => key);
        // _configPath is initialized inside PresetService. It won't affect our validation logic tests.
        _presetService = new PresetService(_loggerMock.Object, _languageMock.Object);
    }

    [Fact]
    public void ValidPresetFile_WhenPresetsIsNull_ShouldCreateListAndReturnFalse()
    {
        // Arrange
        _presetService.Config.Presets = null!; // Simulate corrupted state

        // Act
        var result = _presetService.ValidPresetFile();

        // Assert
        Assert.False(result); // isModified == true -> returns false
        Assert.NotNull(_presetService.Config.Presets);
        Assert.Single(_presetService.Config.Presets);
        Assert.Equal("Preset_1", _presetService.Config.LastSelectedPresetName);
    }

    [Fact]
    public void ValidPresetFile_WhenPresetsIsEmpty_ShouldAddDefaultPresetAndReturnFalse()
    {
        // Arrange
        _presetService.Config.Presets.Clear();

        // Act
        var result = _presetService.ValidPresetFile();

        // Assert
        Assert.False(result);
        Assert.Single(_presetService.Config.Presets);
        Assert.Equal("Preset_1", _presetService.Config.LastSelectedPresetName);
    }

    [Fact]
    public void ValidPresetFile_WhenLastSelectedPresetNameNotFound_ShouldUpdateAndReturnFalse()
    {
        // Arrange
        _presetService.Config.Presets.Clear();
        _presetService.Config.Presets.Add(new ConvertPreset { Name = "MyPreset" });
        _presetService.Config.LastSelectedPresetName = "NonExistentPreset";

        // Act
        var result = _presetService.ValidPresetFile();

        // Assert
        Assert.False(result);
        Assert.Equal("MyPreset", _presetService.Config.LastSelectedPresetName);
    }

    [Fact]
    public void ValidPresetFile_WhenConfigIsValid_ShouldReturnTrue()
    {
        // Arrange
        _presetService.Config.Presets.Clear();
        _presetService.Config.Presets.Add(new ConvertPreset { Name = "ValidPreset" });
        _presetService.Config.LastSelectedPresetName = "ValidPreset";

        // Act
        var result = _presetService.ValidPresetFile();

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData(0, false)]    // Under limit
    [InlineData(101, false)]  // Over limit
    [InlineData(50, true)]    // Valid
    public void ValidPresetData_ShouldValidateQuality(int quality, bool expected)
    {
        // Arrange
        _presetService.Config.Presets.Clear();
        var settings = new ConvertSettings { Quality = quality };
        _presetService.Config.Presets.Add(new ConvertPreset { Name = "TestPreset", Settings = settings });
        _presetService.Config.LastSelectedPresetName = "TestPreset";

        // Act
        var result = _presetService.ValidPresetData(out string errorKey);

        // Assert
        Assert.Equal(expected, result);
        if (!expected)
            Assert.Equal("Msg_Error_ConfigInvalid", errorKey);
        else
            Assert.True(string.IsNullOrEmpty(errorKey));
    }

    [Theory]
    [InlineData("INVALID", false)]
    [InlineData("JPEG", true)]
    [InlineData("Avif", true)]
    [InlineData("", false)]
    public void ValidPresetData_ShouldValidateStandardTargetFormat(string format, bool expected)
    {
        // Arrange
        _presetService.Config.Presets.Clear();
        var settings = new ConvertSettings { StandardTargetFormat = format, AnimationTargetFormat = "GIF" };
        _presetService.Config.Presets.Add(new ConvertPreset { Name = "TestPreset", Settings = settings });
        _presetService.Config.LastSelectedPresetName = "TestPreset";

        // Act
        var result = _presetService.ValidPresetData(out string errorKey);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ValidPresetData_WhenSettingsIsNull_ShouldReturnFalse()
    {
        // Arrange
        _presetService.Config.Presets.Clear();
        _presetService.Config.Presets.Add(new ConvertPreset { Name = "TestPreset", Settings = null! });
        _presetService.Config.LastSelectedPresetName = "TestPreset";

        // Act
        var result = _presetService.ValidPresetData(out string errorKey);

        // Assert
        Assert.False(result);
        Assert.Equal("Msg_Error_ConfigInvalid", errorKey);
    }
}
