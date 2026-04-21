using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NetVips;
using PixConvert.Models;
using PixConvert.Services;
using PixConvert.Services.Providers;
using Xunit;

namespace PixConvert.Tests;

public class NetVipsProviderTests : IDisposable
{
    private readonly TempDirectoryFixture _tempDirectory = new("NetVipsTests_");
    private readonly NetVipsProvider _provider;
    private readonly string _inputPath;

    public NetVipsProviderTests()
    {
        _provider = new NetVipsProvider(new FakeLanguageService(), NullLogger<NetVipsProvider>.Instance);
        _inputPath = _tempDirectory.CreatePath("input.png");
        TestImageFactory.CreateTransparentPng(_inputPath);
    }

    [Theory]
    [InlineData(false, 80, WebpPresetMode.Drawing, false, "animated.webp")]
    [InlineData(true, 100, WebpPresetMode.Default, true, "animated.webp")]
    public async Task ConvertAsync_GifToWebp_ShouldRespectWebpOptionBranches(
        bool lossless,
        int quality,
        WebpPresetMode preset,
        bool preserveTransparentPixels,
        string expectedFileName)
    {
        string gifPath = TestImageFactory.CreateAnimatedGif(_tempDirectory.CreatePath("animated.gif"), 2);
        var file = new FileItem { Path = gifPath, FileSignature = "GIF", IsAnimation = true };
        var settings = new ConvertSettings
        {
            AnimationTargetFormat = "WEBP",
            AnimationLossless = lossless,
            AnimationQuality = quality,
            AnimationWebpEncodingEffort = 6,
            AnimationWebpPreset = preset,
            AnimationWebpPreserveTransparentPixels = preserveTransparentPixels,
            SaveLocation = SaveLocationType.SameAsOriginal,
            FolderMethod = SaveFolderMethod.NoFolder,
            OverwritePolicy = OverwritePolicy.Suffix
        };

        string expectedPath = _tempDirectory.CreatePath(expectedFileName);
        var result = await _provider.ConvertAsync(file, settings, new ConversionSession(), CancellationToken.None);

        ProviderTestAssertions.AssertSuccessResult(result, expectedPath);

        using var output = Image.NewFromFile(expectedPath);
        Assert.NotNull(output);
    }

    [Fact]
    public async Task ConvertAsync_GifToGif_WithCustomGifOptions_ShouldProduceValidFile()
    {
        string gifPath = TestImageFactory.CreateAnimatedGif(_tempDirectory.CreatePath("animated.gif"), 3);
        var file = new FileItem { Path = gifPath, FileSignature = "GIF", IsAnimation = true };
        var settings = new ConvertSettings
        {
            AnimationTargetFormat = "GIF",
            AnimationGifPalettePreset = GifPalettePreset.Simple,
            AnimationGifEncodingEffort = 9,
            AnimationGifInterframeMaxError = 4.0,
            AnimationGifInterpaletteMaxError = 2.0,
            SaveLocation = SaveLocationType.SameAsOriginal,
            FolderMethod = SaveFolderMethod.NoFolder,
            OverwritePolicy = OverwritePolicy.Suffix
        };

        string expectedPath = _tempDirectory.CreatePath("animated_1.gif");
        var result = await _provider.ConvertAsync(file, settings, new ConversionSession(), CancellationToken.None);

        ProviderTestAssertions.AssertSuccessResult(result, expectedPath);

        using var output = Image.NewFromFile(expectedPath);
        Assert.NotNull(output);
    }

    [Fact]
    public async Task ConvertAsync_WhenAlphaAndJpegTarget_ShouldFlattenBackground()
    {
        var file = new FileItem { Path = _inputPath, FileSignature = "PNG" };
        var settings = new ConvertSettings
        {
            StandardTargetFormat = "JPEG",
            SaveLocation = SaveLocationType.SameAsOriginal,
            FolderMethod = SaveFolderMethod.NoFolder,
            StandardBackgroundColor = "#000000"
        };

        string expectedPath = _tempDirectory.CreatePath("input.jpg");
        var result = await _provider.ConvertAsync(file, settings, new ConversionSession(), CancellationToken.None);
        ProviderTestAssertions.AssertSuccessResult(result, expectedPath);

        using var output = Image.NewFromFile(expectedPath);
        Assert.False(output.HasAlpha());
    }

    [Fact]
    public async Task ConvertAsync_WhenCancelled_ShouldThrow()
    {
        var file = new FileItem { Path = _inputPath, FileSignature = "PNG" };
        var settings = new ConvertSettings { StandardTargetFormat = "WEBP" };
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await _provider.ConvertAsync(file, settings, new ConversionSession(), cts.Token));
    }

    [Fact]
    public async Task ConvertAsync_WhenFileIsCorrupted_ShouldThrowWithoutMutatingFile()
    {
        string corruptPath = _tempDirectory.CreatePath("corrupt.png");
        File.WriteAllText(corruptPath, "Not an image data");
        var file = new FileItem { Path = corruptPath, FileSignature = "PNG" };
        var settings = new ConvertSettings { StandardTargetFormat = "JPEG" };

        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await _provider.ConvertAsync(file, settings, new ConversionSession(), CancellationToken.None));

        ProviderTestAssertions.AssertProviderDidNotMutateFileItem(file);
    }

    public void Dispose()
    {
        _tempDirectory.Dispose();
    }
}
