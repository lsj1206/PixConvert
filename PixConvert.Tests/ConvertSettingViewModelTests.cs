using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PixConvert.Models;
using PixConvert.Services;
using PixConvert.Services.Interfaces;
using PixConvert.ViewModels;
using Xunit;

namespace PixConvert.Tests;

public class ConvertSettingViewModelTests
{
    private static ConvertSettingViewModel CreateViewModel()
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
            presetMock.Object);
    }

    [Fact]
    public void AnimationWebpOptions_WhenAnimationTargetIsNull_ShouldBeHidden()
    {
        var vm = CreateViewModel();

        vm.AnimationTargetFormat = null;

        Assert.False(vm.AnimationShowWebpEncodingEffort);
        Assert.False(vm.AnimationShowWebpPreset);
        Assert.False(vm.AnimationShowWebpPreserveTransparentPixels);
    }

    [Fact]
    public void AnimationWebpOptions_WhenAnimationTargetIsGif_ShouldBeHidden()
    {
        var vm = CreateViewModel();

        vm.AnimationTargetFormat = "GIF";

        Assert.False(vm.AnimationShowWebpEncodingEffort);
        Assert.False(vm.AnimationShowWebpPreset);
        Assert.False(vm.AnimationShowWebpPreserveTransparentPixels);
    }

    [Fact]
    public void AnimationWebpOptions_WhenLossyWebp_ShouldShowEffortAndPreset()
    {
        var vm = CreateViewModel();

        vm.AnimationTargetFormat = "WEBP";
        vm.AnimationLossless = false;

        Assert.True(vm.AnimationShowWebpEncodingEffort);
        Assert.True(vm.AnimationShowWebpPreset);
        Assert.False(vm.AnimationShowWebpPreserveTransparentPixels);
    }

    [Fact]
    public void AnimationWebpOptions_WhenLosslessWebp_ShouldShowEffortAndPreserveTransparentPixels()
    {
        var vm = CreateViewModel();

        vm.AnimationTargetFormat = "WEBP";
        vm.AnimationLossless = true;

        Assert.True(vm.AnimationShowWebpEncodingEffort);
        Assert.False(vm.AnimationShowWebpPreset);
        Assert.True(vm.AnimationShowWebpPreserveTransparentPixels);
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
}
