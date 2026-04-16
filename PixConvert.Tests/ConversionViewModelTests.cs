using Microsoft.Extensions.Logging;
using PixConvert.Models;
using PixConvert.Services;
using PixConvert.Services.Interfaces;
using PixConvert.Services.Providers;
using PixConvert.ViewModels;
using Moq;
using Xunit;

namespace PixConvert.Tests;

public class ConversionViewModelTests
{
    private readonly Mock<IPresetService> _mockPreset;
    private readonly Mock<IDialogService> _mockDialog;
    private readonly FileListViewModel _fileListVm;
    private readonly ConversionViewModel _vm;

    public ConversionViewModelTests()
    {
        var mockLang = new Mock<ILanguageService>();
        var mockLogger = new Mock<ILogger<ConversionViewModel>>();
        _mockPreset = new Mock<IPresetService>();
        var mockSnackbar = new Mock<ISnackbarService>();
        _mockDialog = new Mock<IDialogService>();

        mockLang.Setup(l => l.GetString(It.IsAny<string>())).Returns<string>(s => s);
        var presetConfig = new PresetConfig();
        presetConfig.Presets.Add(new ConvertPreset { Name = "Default", Settings = new ConvertSettings() });
        _mockPreset.Setup(p => p.Config).Returns(presetConfig);
        _mockPreset.Setup(p => p.ActivePreset).Returns((ConvertPreset?)null);
        _mockPreset.Setup(p => p.SaveAsync()).ReturnsAsync(true);

        var mockSkia = new Mock<SkiaSharpProvider>(mockLang.Object, new Mock<ILogger<SkiaSharpProvider>>().Object);
        var mockVips = new Mock<NetVipsProvider>(mockLang.Object, new Mock<ILogger<NetVipsProvider>>().Object);
        mockSkia.As<IProviderService>().Setup(p => p.Name).Returns("SkiaSharp");
        mockVips.As<IProviderService>().Setup(p => p.Name).Returns("NetVips");
        var engineSelector = new EngineSelector(mockSkia.Object, mockVips.Object);

        _fileListVm = new FileListViewModel(mockLang.Object, new Mock<ILogger<FileListViewModel>>().Object);

        _vm = new ConversionViewModel(
            mockLogger.Object,
            mockLang.Object,
            _mockDialog.Object,
            mockSnackbar.Object,
            _mockPreset.Object,
            _fileListVm,
            engineSelector,
            () => CreateConvertSettingViewModel(mockLang));
    }

    [Fact]
    public void DefaultState_ShouldInitializeAsEmptyPreset()
    {
        Assert.Equal("Converting_SelectPreset", _vm.ActivePresetName);
        Assert.False(_vm.IsActivePresetValid);
    }

    [Fact]
    public void Items_ShouldExposeUnderlyingFileListItems()
    {
        _fileListVm.AddItem(new FileItem { Path = @"C:\test.png" });

        Assert.Single(_vm.Items);
        Assert.Equal(@"C:\test.png", _vm.Items[0].Path);
    }

    [Fact]
    public void HasFailures_ShouldTrackFailCount()
    {
        Assert.False(_vm.HasFailures);

        _vm.FailCount = 1;

        Assert.True(_vm.HasFailures);
    }

    [Fact]
    public void Commands_WhenConverting_ShouldDisableStartAndEnableCancel()
    {
        _vm.CurrentStatus = AppStatus.Converting;

        Assert.False(_vm.OpenConvertSettingCommand.CanExecute(null));
        Assert.False(_vm.ConvertFilesCommand.CanExecute(null));
        Assert.True(_vm.CancelConvertCommand.CanExecute(null));
    }

    [Fact]
    public async Task OpenConvertSettingCommand_WhenConfirmed_ShouldSavePreset()
    {
        _mockDialog
            .Setup(service => service.ShowConvertSettingDialogAsync(It.IsAny<ConvertSettingViewModel>()))
            .ReturnsAsync(true);

        await _vm.OpenConvertSettingCommand.ExecuteAsync(null);

        _mockDialog.Verify(
            service => service.ShowConvertSettingDialogAsync(It.IsAny<ConvertSettingViewModel>()),
            Times.Once);
        _mockPreset.Verify(service => service.SaveAsync(), Times.Once);
    }

    [Fact]
    public void BuildStandardOptionsSummary_WhenJpeg_ShouldIncludeQualityChromaAndBackground()
    {
        var settings = new ConvertSettings
        {
            StandardTargetFormat = "JPEG",
            StandardQuality = 90,
            StandardJpegChromaSubsampling = JpegChromaSubsamplingMode.Chroma444,
            StandardBackgroundColor = "#101010"
        };
        var files = new List<FileItem> { new() { Path = @"C:\test.png", FileSignature = "PNG" } };

        string summary = ConversionViewModel.BuildStandardOptionsSummary(settings, files, Key);

        Assert.Equal(
            string.Join(Environment.NewLine,
                "Setting_Quality 90",
                "Setting_ChromaSubsampling Setting_Subsampling_444",
                "Converting_BgColor #101010"),
            summary);
    }

    [Fact]
    public void BuildStandardOptionsSummary_WhenJpeg422WithAvifInput_ShouldShowNetVipsFallback()
    {
        var settings = new ConvertSettings
        {
            StandardTargetFormat = "JPEG",
            StandardQuality = 90,
            StandardJpegChromaSubsampling = JpegChromaSubsamplingMode.Chroma422,
            StandardBackgroundColor = "#FFFFFF"
        };
        var files = new List<FileItem> { new() { Path = @"C:\test.avif", FileSignature = "AVIF" } };

        string summary = ConversionViewModel.BuildStandardOptionsSummary(settings, files, Key);

        Assert.Contains("Setting_ChromaSubsampling Converting_Jpeg422AvifAuto", summary);
    }

    [Fact]
    public void BuildStandardOptionsSummary_WhenAvifLossless_ShouldExcludeQualityAndChroma()
    {
        var settings = new ConvertSettings
        {
            StandardTargetFormat = "AVIF",
            StandardLossless = true,
            StandardQuality = 90,
            StandardAvifEncodingEffort = 9,
            StandardAvifBitDepth = AvifBitDepthMode.Bit10
        };
        var files = new List<FileItem> { new() { Path = @"C:\test.png", FileSignature = "PNG" } };

        string summary = ConversionViewModel.BuildStandardOptionsSummary(settings, files, Key);

        Assert.Equal(
            string.Join(Environment.NewLine,
                "Setting_Lossless",
                "Setting_EncodingEffort 9",
                "Setting_BitDepth Setting_BitDepth_10"),
            summary);
    }

    [Fact]
    public void BuildAnimationOptionsSummary_WhenWebpLossy_ShouldIncludeQualityEffortAndPreset()
    {
        var settings = new ConvertSettings
        {
            AnimationTargetFormat = "WEBP",
            AnimationLossless = false,
            AnimationQuality = 80,
            AnimationWebpEncodingEffort = 6,
            AnimationWebpPreset = WebpPresetMode.Photo
        };
        var files = new List<FileItem> { new() { Path = @"C:\test.gif", FileSignature = "GIF", IsAnimation = true } };

        string summary = ConversionViewModel.BuildAnimationOptionsSummary(settings, files, Key);

        Assert.Equal(
            string.Join(Environment.NewLine,
                "Setting_Quality 80",
                "Setting_EncodingEffort 6",
                "Setting_WebpPreset Setting_WebpPreset_Photo"),
            summary);
    }

    [Fact]
    public void BuildAnimationOptionsSummary_WhenGif_ShouldIncludePaletteAndErrors()
    {
        var settings = new ConvertSettings
        {
            AnimationTargetFormat = "GIF",
            AnimationGifPalettePreset = GifPalettePreset.Balance,
            AnimationGifInterframeMaxError = 1.25,
            AnimationGifInterpaletteMaxError = 2.5
        };
        var files = new List<FileItem> { new() { Path = @"C:\test.gif", FileSignature = "GIF", IsAnimation = true } };

        string summary = ConversionViewModel.BuildAnimationOptionsSummary(settings, files, Key);

        Assert.Equal(
            string.Join(Environment.NewLine,
                "Setting_PalettePreset Setting_GifPalette_Balance",
                "Setting_InterframeMaxError 1.25",
                "Setting_InterpaletteMaxError 2.5"),
            summary);
    }

    [Fact]
    public void BuildAnimationOptionsSummary_WhenAnimationTargetIsNull_ShouldReturnEmpty()
    {
        var settings = new ConvertSettings { AnimationTargetFormat = null };
        var files = new List<FileItem> { new() { Path = @"C:\test.gif", FileSignature = "GIF", IsAnimation = true } };

        string summary = ConversionViewModel.BuildAnimationOptionsSummary(settings, files, Key);

        Assert.Equal(string.Empty, summary);
    }

    private static string Key(string key) => key;

    private ConvertSettingViewModel CreateConvertSettingViewModel(Mock<ILanguageService> language)
    {
        return new ConvertSettingViewModel(
            language.Object,
            new Mock<ILogger<ConvertSettingViewModel>>().Object,
            _mockPreset.Object,
            new Mock<IPathPickerService>().Object);
    }
}
