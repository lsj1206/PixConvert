using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PixConvert.Models;
using PixConvert.Services;
using PixConvert.Services.Interfaces;
using PixConvert.ViewModels;
using System.Linq;
using Xunit;

namespace PixConvert.Tests;

public class ConvertSettingViewModelTests
{
    public static IEnumerable<object[]> AnimationVisibilityCases()
    {
        yield return new object[] { null!, false, false, false, false, false };
        yield return new object[] { "GIF", false, false, false, false, true };
        yield return new object[] { "WEBP", false, true, true, false, false };
        yield return new object[] { "WEBP", true, true, false, true, false };
    }

    private static ConvertSettingViewModel CreateViewModel(Mock<IPathPickerService>? pathPicker = null)
    {
        var languageMock = new Mock<ILanguageService>();
        languageMock.Setup(x => x.GetString(It.IsAny<string>())).Returns((string key) => key);

        var presetConfig = new PresetConfig
        {
            LastSelectedPresetName = "Preset_1"
        };
        presetConfig.Presets.Add(new ConvertPreset { Name = "Preset_1", Settings = new ConvertSettings() });

        var presetMock = new Mock<IPresetService>();
        presetMock.SetupGet(x => x.Config).Returns(presetConfig);

        return new ConvertSettingViewModel(
            languageMock.Object,
            NullLogger<ConvertSettingViewModel>.Instance,
            presetMock.Object,
            (pathPicker ?? new Mock<IPathPickerService>()).Object);
    }

    [Theory]
    [MemberData(nameof(AnimationVisibilityCases))]
    public void AnimationOptionVisibility_ShouldMatchTargetAndLosslessMode(
        string? targetFormat,
        bool lossless,
        bool showWebpEffort,
        bool showWebpPreset,
        bool showPreserveTransparentPixels,
        bool showGifEffort)
    {
        var vm = CreateViewModel();

        vm.AnimationTargetFormat = targetFormat;
        vm.AnimationLossless = lossless;

        Assert.Equal(showWebpEffort, vm.AnimationShowWebpEncodingEffort);
        Assert.Equal(showWebpPreset, vm.AnimationShowWebpPreset);
        Assert.Equal(showPreserveTransparentPixels, vm.AnimationShowWebpPreserveTransparentPixels);
        Assert.Equal(showGifEffort, vm.AnimationShowGifEncodingEffort);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(4, 4)]
    [InlineData(7, 6)]
    public void AnimationWebpEncodingEffort_ShouldClampToSupportedRange(int input, int expected)
    {
        var vm = CreateViewModel();

        vm.AnimationWebpEncodingEffort = input;

        Assert.Equal(expected, vm.AnimationWebpEncodingEffort);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(6, 6)]
    [InlineData(10, 9)]
    public void AnimationGifEncodingEffort_ShouldClampToSupportedRange(int input, int expected)
    {
        var vm = CreateViewModel();

        vm.AnimationGifEncodingEffort = input;

        Assert.Equal(expected, vm.AnimationGifEncodingEffort);
    }

    [Fact]
    public void ChangeOutputPathCommand_WhenFolderSelected_ShouldUpdateCustomOutputPath()
    {
        var pathPicker = new Mock<IPathPickerService>();
        pathPicker
            .Setup(service => service.PickFolder("Dlg_Title_SelectOutputPath"))
            .Returns(@"C:\Output");
        var vm = CreateViewModel(pathPicker);

        vm.ChangeOutputPathCommand.Execute(null);

        Assert.Equal(@"C:\Output", vm.CustomOutputPath);
    }

    [Fact]
    public void StandardTargetTag_WhenDeselected_ShouldRestoreRequiredSelection()
    {
        var vm = CreateViewModel();
        var jpegTag = vm.StandardTargetTags.Single(tag => tag.Format == "JPEG");

        jpegTag.IsSelected = false;

        Assert.True(jpegTag.IsSelected);
        Assert.Equal("JPEG", vm.StandardTargetFormat);
    }

    [Fact]
    public void AnimationTargetTag_WhenDeselected_ShouldAllowEmptySelection()
    {
        var vm = CreateViewModel();
        var gifTag = vm.AnimationTargetTags.Single(tag => tag.Format == "GIF");

        gifTag.IsSelected = false;

        Assert.False(gifTag.IsSelected);
        Assert.Null(vm.AnimationTargetFormat);
    }
}
