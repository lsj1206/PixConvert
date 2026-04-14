using System.Linq;
using Microsoft.Extensions.Logging;
using Moq;
using PixConvert.Models;
using PixConvert.Services;
using Xunit;

namespace PixConvert.Tests;

public class PresetServiceTests
{
    private readonly PresetService _presetService;

    public PresetServiceTests()
    {
        var loggerMock = new Mock<ILogger<PresetService>>();
        var languageMock = new Mock<ILanguageService>();
        languageMock.Setup(x => x.GetString(It.IsAny<string>())).Returns((string key) => key);
        _presetService = new PresetService(loggerMock.Object, languageMock.Object);
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

        var result = _presetService.ValidPresetData(settings, out string errorKey);

        Assert.Equal(expected, result);
        if (!expected)
            Assert.Equal("Msg_Error_ConfigInvalid", errorKey);
        else
            Assert.True(string.IsNullOrEmpty(errorKey));
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

        var result = _presetService.ValidPresetData(settings, out string errorKey);

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

        var result = _presetService.ValidPresetData(settings, out string errorKey);

        Assert.False(result);
        Assert.Equal("Msg_Error_ConfigInvalid", errorKey);
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

        var result = _presetService.ValidPresetData(settings, out string errorKey);

        Assert.False(result);
        Assert.Equal("Msg_Error_ConfigInvalid", errorKey);
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

        var result = _presetService.ValidPresetData(settings, out string errorKey);

        Assert.Equal(expected, result);
        if (!expected)
            Assert.Equal("Msg_Error_ConfigInvalid", errorKey);
        else
            Assert.True(string.IsNullOrEmpty(errorKey));
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

        var result = _presetService.ValidPresetData(settings, out string errorKey);

        Assert.False(result);
        Assert.Equal("Msg_Error_ConfigInvalid", errorKey);
    }

    [Fact]
    public void ConvertSettings_DefaultJpegSubsampling_ShouldBe444()
    {
        var settings = new ConvertSettings();

        Assert.Equal(JpegChromaSubsamplingMode.Chroma444, settings.StandardJpegChromaSubsampling);
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

        var result = _presetService.ValidPresetData(settings, out string errorKey);

        Assert.True(result);
        Assert.True(string.IsNullOrEmpty(errorKey));
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

        var result = _presetService.ValidPresetData(settings, out string errorKey);

        Assert.Equal(expected, result);
        if (!expected)
            Assert.Equal("Msg_Error_ConfigInvalid", errorKey);
        else
            Assert.True(string.IsNullOrEmpty(errorKey));
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

        var result = _presetService.ValidPresetData(settings, out string errorKey);

        Assert.Equal(expected, result);
        if (!expected)
            Assert.Equal("Msg_Error_ConfigInvalid", errorKey);
        else
            Assert.True(string.IsNullOrEmpty(errorKey));
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

        var result = _presetService.ValidPresetData(settings, out string errorKey);

        Assert.Equal(expected, result);
        if (!expected)
            Assert.Equal("Msg_Error_ConfigInvalid", errorKey);
        else
            Assert.True(string.IsNullOrEmpty(errorKey));
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

        var result = _presetService.ValidPresetData(settings, out string errorKey);

        Assert.False(result);
        Assert.Equal("Msg_Error_ConfigInvalid", errorKey);
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

        var result = _presetService.ValidPresetData(settings, out string errorKey);

        Assert.Equal(expected, result);
        if (!expected)
            Assert.Equal("Msg_Error_ConfigInvalid", errorKey);
        else
            Assert.True(string.IsNullOrEmpty(errorKey));
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

        var result = _presetService.ValidPresetData(settings, out string errorKey);

        Assert.False(result);
        Assert.Equal("Msg_Error_ConfigInvalid", errorKey);
    }

    [Fact]
    public void AddPreset_WhenNewName_ShouldIncreaseCount()
    {
        int before = _presetService.Config.Presets.Count;

        _presetService.AddPreset("NewPreset", new ConvertSettings());

        Assert.Equal(before + 1, _presetService.Config.Presets.Count);
        Assert.Contains(_presetService.Config.Presets, p => p.Name == "NewPreset");
    }

    [Fact]
    public void AddPreset_WhenDuplicateName_ShouldNotAdd()
    {
        _presetService.Config.Presets.Clear();
        _presetService.Config.Presets.Add(new ConvertPreset { Name = "Existing" });
        int before = _presetService.Config.Presets.Count;

        _presetService.AddPreset("Existing", new ConvertSettings());

        Assert.Equal(before, _presetService.Config.Presets.Count);
    }

    [Fact]
    public void RemovePreset_WhenExists_ShouldDecreaseCount()
    {
        _presetService.Config.Presets.Clear();
        _presetService.Config.Presets.Add(new ConvertPreset { Name = "ToRemove" });
        _presetService.Config.Presets.Add(new ConvertPreset { Name = "Remaining" });

        _presetService.RemovePreset("ToRemove");

        Assert.Single(_presetService.Config.Presets);
        Assert.DoesNotContain(_presetService.Config.Presets, p => p.Name == "ToRemove");
    }

    [Fact]
    public void RemovePreset_WhenNotExists_ShouldNotThrow()
    {
        _presetService.Config.Presets.Clear();
        _presetService.Config.Presets.Add(new ConvertPreset { Name = "Only" });

        var ex = Record.Exception(() => _presetService.RemovePreset("NonExistent"));

        Assert.Null(ex);
        Assert.Single(_presetService.Config.Presets);
    }

    [Fact]
    public void RenamePreset_WhenValid_ShouldChangeName()
    {
        _presetService.Config.Presets.Clear();
        _presetService.Config.Presets.Add(new ConvertPreset { Name = "OldName" });
        _presetService.Config.LastSelectedPresetName = "OldName";

        _presetService.RenamePreset("OldName", "NewName");

        Assert.Contains(_presetService.Config.Presets, p => p.Name == "NewName");
        Assert.DoesNotContain(_presetService.Config.Presets, p => p.Name == "OldName");
    }

    [Fact]
    public void RenamePreset_WhenSameAsLastSelected_ShouldUpdateLastSelected()
    {
        _presetService.Config.Presets.Clear();
        _presetService.Config.Presets.Add(new ConvertPreset { Name = "Active" });
        _presetService.Config.LastSelectedPresetName = "Active";

        _presetService.RenamePreset("Active", "ActiveRenamed");

        Assert.Equal("ActiveRenamed", _presetService.Config.LastSelectedPresetName);
    }

    [Fact]
    public void RenamePreset_WhenNewNameAlreadyExists_ShouldNotRename()
    {
        _presetService.Config.Presets.Clear();
        _presetService.Config.Presets.Add(new ConvertPreset { Name = "A" });
        _presetService.Config.Presets.Add(new ConvertPreset { Name = "B" });

        _presetService.RenamePreset("A", "B");

        Assert.Contains(_presetService.Config.Presets, p => p.Name == "A");
    }

    [Fact]
    public void CopyPreset_WhenValid_ShouldAddNewPreset()
    {
        _presetService.Config.Presets.Clear();
        var original = new ConvertPreset { Name = "Original", Settings = new ConvertSettings { StandardQuality = 90 } };
        _presetService.Config.Presets.Add(original);

        _presetService.CopyPreset("Original", "Original_Copy");

        Assert.Equal(2, _presetService.Config.Presets.Count);
        Assert.Contains(_presetService.Config.Presets, p => p.Name == "Original_Copy");
    }

    [Fact]
    public void CopyPreset_ShouldCreateDeepCopy_OriginalChangeDoesNotAffectCopy()
    {
        _presetService.Config.Presets.Clear();
        var original = new ConvertPreset { Name = "Source", Settings = new ConvertSettings { StandardQuality = 80 } };
        _presetService.Config.Presets.Add(original);

        _presetService.CopyPreset("Source", "Dest");
        original.Settings.StandardQuality = 50;

        var copy = _presetService.Config.Presets.First(p => p.Name == "Dest");
        Assert.Equal(80, copy.Settings.StandardQuality);
    }
}
