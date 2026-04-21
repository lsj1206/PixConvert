using PixConvert.Models;
using PixConvert.ViewModels;
using Xunit;

namespace PixConvert.Tests;

public class ConversionViewModelSummaryTests
{
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

        string summary = ConversionSummaryBuilder.BuildStandardOptionsSummary(settings, files, ConversionViewModelTestHarness.Key);

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

        string summary = ConversionSummaryBuilder.BuildStandardOptionsSummary(settings, files, ConversionViewModelTestHarness.Key);

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

        string summary = ConversionSummaryBuilder.BuildStandardOptionsSummary(settings, files, ConversionViewModelTestHarness.Key);

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

        string summary = ConversionSummaryBuilder.BuildAnimationOptionsSummary(settings, files, ConversionViewModelTestHarness.Key);

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
            AnimationGifEncodingEffort = 9,
            AnimationGifInterframeMaxError = 1.25,
            AnimationGifInterpaletteMaxError = 2.5
        };
        var files = new List<FileItem> { new() { Path = @"C:\test.gif", FileSignature = "GIF", IsAnimation = true } };

        string summary = ConversionSummaryBuilder.BuildAnimationOptionsSummary(settings, files, ConversionViewModelTestHarness.Key);

        Assert.Equal(
            string.Join(Environment.NewLine,
                "Setting_PalettePreset Setting_GifPalette_Balance",
                "Setting_EncodingEffort 9",
                "Setting_InterframeMaxError 1.25",
                "Setting_InterpaletteMaxError 2.5"),
            summary);
    }

    [Fact]
    public void BuildAnimationOptionsSummary_WhenAnimationTargetIsNull_ShouldReturnEmpty()
    {
        var settings = new ConvertSettings { AnimationTargetFormat = null };
        var files = new List<FileItem> { new() { Path = @"C:\test.gif", FileSignature = "GIF", IsAnimation = true } };

        string summary = ConversionSummaryBuilder.BuildAnimationOptionsSummary(settings, files, ConversionViewModelTestHarness.Key);

        Assert.Equal(string.Empty, summary);
    }

    [Fact]
    public void BuildTargetFormatSummary_WhenInputsContainStaticAndAnimation_ShouldReturnBothFormats()
    {
        var settings = new ConvertSettings
        {
            StandardTargetFormat = "JPEG",
            AnimationTargetFormat = "WEBP"
        };
        var files = new List<FileItem>
        {
            new() { Path = @"C:\test.png", FileSignature = "PNG" },
            new() { Path = @"C:\test.gif", FileSignature = "GIF", IsAnimation = true }
        };

        string summary = ConversionSummaryBuilder.BuildTargetFormatSummary(settings, files);

        Assert.Equal("JPEG / WEBP", summary);
    }

    [Fact]
    public void BuildSaveMethodSummary_WhenCreatingFolder_ShouldReturnFolderName()
    {
        var settings = new ConvertSettings
        {
            FolderMethod = SaveFolderMethod.CreateFolder,
            OutputSubFolderName = "Converted"
        };

        string summary = ConversionSummaryBuilder.BuildSaveMethodSummary(settings, ConversionViewModelTestHarness.Key);

        Assert.Equal("Converted", summary);
    }

    [Fact]
    public void BuildSaveMethodSummary_WhenNotCreatingFolder_ShouldUseLocalizedMethodKey()
    {
        var settings = new ConvertSettings
        {
            FolderMethod = SaveFolderMethod.NoFolder
        };

        string summary = ConversionSummaryBuilder.BuildSaveMethodSummary(settings, ConversionViewModelTestHarness.Key);

        Assert.Equal("Setting_SaveMethod_NoFolder", summary);
    }

    [Fact]
    public void BuildSaveLocationSummary_WhenSameAsOriginal_ShouldPrefixDisplayAndClearTooltip()
    {
        var settings = new ConvertSettings
        {
            SaveLocation = SaveLocationType.SameAsOriginal
        };

        SaveLocationSummary summary = ConversionSummaryBuilder.BuildSaveLocationSummary(
            settings,
            key => key == "Setting_SaveLocation_Same" ? "Same Folder" : key);

        Assert.Equal("...Same Folder", summary.DisplayText);
        Assert.Equal(string.Empty, summary.TooltipText);
    }

    [Fact]
    public void BuildSaveLocationSummary_WhenCustomPathHasTrailingSeparator_ShouldUseLastFolderName()
    {
        var settings = new ConvertSettings
        {
            SaveLocation = SaveLocationType.Custom,
            CustomOutputPath = @"D:\Output\Nested\"
        };

        SaveLocationSummary summary = ConversionSummaryBuilder.BuildSaveLocationSummary(settings, ConversionViewModelTestHarness.Key);

        Assert.Equal("...Nested", summary.DisplayText);
        Assert.Equal(@"D:\Output\Nested\", summary.TooltipText);
    }
}
