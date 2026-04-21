using System.Linq;
using Microsoft.Extensions.Logging;
using Moq;
using PixConvert.Models;
using PixConvert.Services;
using Xunit;

namespace PixConvert.Tests;

public class PresetServiceMutationTests
{
    private readonly PresetService _presetService;

    public PresetServiceMutationTests()
    {
        var loggerMock = new Mock<ILogger<PresetService>>();
        _presetService = new PresetService(loggerMock.Object, new FakeLanguageService());
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
