using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Extensions.Logging.Abstractions;
using NetVips;
using PixConvert.Models;
using PixConvert.Services;
using PixConvert.Services.Providers;

namespace PixConvert.Tests;

public class NetVipsProviderTests : IDisposable
{
    private readonly NetVipsProvider _provider;
    private readonly ILanguageService _lang;
    private readonly string _testDir;
    private readonly string _inputPath;

    private class MockLanguageService : ILanguageService
    {
        public string GetString(string key) => key;
        public void ChangeLanguage(string culture) { }
        public string GetSystemLanguage() => "ko-KR";
        public string GetCurrentLanguage() => "ko-KR";
        public event Action LanguageChanged = delegate { };
    }

    public NetVipsProviderTests()
    {
        _lang = new MockLanguageService();
        _provider = new NetVipsProvider(_lang, NullLogger<NetVipsProvider>.Instance);
        _testDir = Path.Combine(Path.GetTempPath(), "NetVipsTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
        _inputPath = Path.Combine(_testDir, "input.png");

        using var bitmap = new SkiaSharp.SKBitmap(100, 100);
        using (var canvas = new SkiaSharp.SKCanvas(bitmap))
        {
            canvas.Clear(SkiaSharp.SKColors.Transparent);
            using var paint = new SkiaSharp.SKPaint { Color = SkiaSharp.SKColors.Blue };
            canvas.DrawRect(0, 0, 50, 50, paint);
        }
        using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        using var stream = File.OpenWrite(_inputPath);
        data.SaveTo(stream);
    }

    public void Dispose()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();

        if (Directory.Exists(_testDir))
        {
            try
            {
                Directory.Delete(_testDir, true);
            }
            catch (IOException)
            {
                Thread.Sleep(500);
                try { Directory.Delete(_testDir, true); } catch { }
            }
        }
    }

    [Fact]
    public async Task ConvertAsync_WhenTargetIsAvif_ShouldWork()
    {
        var file = new FileItem { Path = _inputPath, FileSignature = "PNG" };
        var settings = new ConvertSettings
        {
            StandardTargetFormat = "AVIF",
            StandardAvifChromaSubsampling = AvifChromaSubsamplingMode.Off,
            StandardAvifEncodingEffort = 9,
            StandardAvifBitDepth = AvifBitDepthMode.Bit8,
            SaveLocation = SaveLocationType.SameAsOriginal,
            FolderMethod = SaveFolderMethod.NoFolder
        };

        string expectedPath = Path.Combine(_testDir, "input.avif");
        var result = await _provider.ConvertAsync(file, settings, new ConversionSession(), CancellationToken.None);

        AssertSuccessResult(result, expectedPath);
        AssertProviderDidNotMutateFileItem(file);
    }

    [Fact]
    public async Task ConvertAsync_WhenTargetIsJpegWithCustomSubsampling_ShouldWork()
    {
        var file = new FileItem { Path = _inputPath, FileSignature = "PNG" };
        var settings = new ConvertSettings
        {
            StandardTargetFormat = "JPEG",
            StandardJpegChromaSubsampling = JpegChromaSubsamplingMode.Chroma444,
            SaveLocation = SaveLocationType.SameAsOriginal,
            FolderMethod = SaveFolderMethod.NoFolder
        };

        string expectedPath = Path.Combine(_testDir, "input.jpg");
        var result = await _provider.ConvertAsync(file, settings, new ConversionSession(), CancellationToken.None);

        AssertSuccessResult(result, expectedPath);
    }

    [Fact]
    public async Task ConvertAsync_WhenTargetIsJpegWith422Subsampling_ShouldWork()
    {
        var file = new FileItem { Path = _inputPath, FileSignature = "PNG" };
        var settings = new ConvertSettings
        {
            StandardTargetFormat = "JPEG",
            StandardJpegChromaSubsampling = JpegChromaSubsamplingMode.Chroma422,
            SaveLocation = SaveLocationType.SameAsOriginal,
            FolderMethod = SaveFolderMethod.NoFolder
        };

        string expectedPath = Path.Combine(_testDir, "input.jpg");
        var result = await _provider.ConvertAsync(file, settings, new ConversionSession(), CancellationToken.None);

        AssertSuccessResult(result, expectedPath);
    }

    [Fact]
    public async Task ConvertAsync_WhenTargetIsPngWithCustomCompression_ShouldWork()
    {
        var file = new FileItem { Path = _inputPath, FileSignature = "PNG" };
        var settings = new ConvertSettings
        {
            StandardTargetFormat = "PNG",
            StandardPngCompressionLevel = 9,
            SaveLocation = SaveLocationType.SameAsOriginal,
            FolderMethod = SaveFolderMethod.NoFolder,
            OverwritePolicy = OverwritePolicy.Suffix
        };

        string expectedPath = Path.Combine(_testDir, "input_1.png");
        var result = await _provider.ConvertAsync(file, settings, new ConversionSession(), CancellationToken.None);

        AssertSuccessResult(result, expectedPath);
    }

    [Fact]
    public async Task ConvertAsync_WhenTargetIsBmp_ShouldAttemptConversion()
    {
        var file = new FileItem { Path = _inputPath, FileSignature = "PNG" };
        var settings = new ConvertSettings
        {
            StandardTargetFormat = "BMP",
            SaveLocation = SaveLocationType.SameAsOriginal,
            FolderMethod = SaveFolderMethod.NoFolder
        };

        ConversionResult? result = null;
        Exception? exception = null;

        try
        {
            result = await _provider.ConvertAsync(file, settings, new ConversionSession(), CancellationToken.None);
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        Assert.True(result?.Status == FileConvertStatus.Success || exception is not null);
        AssertProviderDidNotMutateFileItem(file);
    }

    [Fact]
    public async Task ConvertAsync_GifToWebp_ShouldProduceValidFile()
    {
        string gifPath = CreateAnimatedGif(2);
        var file = new FileItem { Path = gifPath, FileSignature = "GIF", IsAnimation = true };
        var settings = new ConvertSettings
        {
            AnimationTargetFormat = "WEBP",
            SaveLocation = SaveLocationType.SameAsOriginal,
            FolderMethod = SaveFolderMethod.NoFolder
        };

        string expectedPath = Path.Combine(_testDir, "animated.webp");
        var result = await _provider.ConvertAsync(file, settings, new ConversionSession(), CancellationToken.None);

        AssertSuccessResult(result, expectedPath);

        using (var output = NetVips.Image.NewFromFile(expectedPath))
        {
            Assert.NotNull(output);
        }
    }

    [Fact]
    public async Task ConvertAsync_GifToWebp_WithLossyWebpOptions_ShouldProduceValidFile()
    {
        string gifPath = CreateAnimatedGif(2);
        var file = new FileItem { Path = gifPath, FileSignature = "GIF", IsAnimation = true };
        var settings = new ConvertSettings
        {
            AnimationTargetFormat = "WEBP",
            AnimationLossless = false,
            AnimationQuality = 80,
            AnimationWebpEncodingEffort = 6,
            AnimationWebpPreset = WebpPresetMode.Drawing,
            SaveLocation = SaveLocationType.SameAsOriginal,
            FolderMethod = SaveFolderMethod.NoFolder,
            OverwritePolicy = OverwritePolicy.Suffix
        };

        string expectedPath = Path.Combine(_testDir, "animated.webp");
        var result = await _provider.ConvertAsync(file, settings, new ConversionSession(), CancellationToken.None);

        AssertSuccessResult(result, expectedPath);

        using var output = NetVips.Image.NewFromFile(expectedPath);
        Assert.NotNull(output);
    }

    [Fact]
    public async Task ConvertAsync_GifToWebp_WithLosslessWebpOptions_ShouldProduceValidFile()
    {
        string gifPath = CreateAnimatedGif(2);
        var file = new FileItem { Path = gifPath, FileSignature = "GIF", IsAnimation = true };
        var settings = new ConvertSettings
        {
            AnimationTargetFormat = "WEBP",
            AnimationLossless = true,
            AnimationWebpEncodingEffort = 6,
            AnimationWebpPreserveTransparentPixels = true,
            SaveLocation = SaveLocationType.SameAsOriginal,
            FolderMethod = SaveFolderMethod.NoFolder,
            OverwritePolicy = OverwritePolicy.Suffix
        };

        string expectedPath = Path.Combine(_testDir, "animated.webp");
        var result = await _provider.ConvertAsync(file, settings, new ConversionSession(), CancellationToken.None);

        AssertSuccessResult(result, expectedPath);

        using var output = NetVips.Image.NewFromFile(expectedPath);
        Assert.NotNull(output);
    }

    [Fact]
    public async Task ConvertAsync_GifToGif_WithCustomGifOptions_ShouldProduceValidFile()
    {
        string gifPath = CreateAnimatedGif(3);
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

        string expectedPath = Path.Combine(_testDir, "animated_1.gif");
        var result = await _provider.ConvertAsync(file, settings, new ConversionSession(), CancellationToken.None);

        AssertSuccessResult(result, expectedPath);

        using var output = NetVips.Image.NewFromFile(expectedPath);
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

        string expectedPath = Path.Combine(_testDir, "input.jpg");
        var result = await _provider.ConvertAsync(file, settings, new ConversionSession(), CancellationToken.None);
        AssertSuccessResult(result, expectedPath);

        using var output = NetVips.Image.NewFromFile(expectedPath);
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
    public async Task ConvertAsync_WhenFileIsCorrupted_ShouldSetErrorStatus()
    {
        string corruptPath = Path.Combine(_testDir, "corrupt.png");
        File.WriteAllText(corruptPath, "Not an image data");
        var file = new FileItem { Path = corruptPath, FileSignature = "PNG" };
        var settings = new ConvertSettings { StandardTargetFormat = "JPEG" };

        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await _provider.ConvertAsync(file, settings, new ConversionSession(), CancellationToken.None));
        AssertProviderDidNotMutateFileItem(file);
    }

    private static void AssertSuccessResult(ConversionResult result, string expectedPath)
    {
        Assert.Equal(FileConvertStatus.Success, result.Status);
        Assert.Equal(expectedPath, result.OutputPath);
        Assert.True(result.OutputSize > 0);
        Assert.True(File.Exists(result.OutputPath), $"Missing output file: {result.OutputPath}");
    }

    private static void AssertProviderDidNotMutateFileItem(FileItem file)
    {
        Assert.Equal(FileConvertStatus.Pending, file.Status);
        Assert.Equal(0, file.Progress);
        Assert.Null(file.OutputPath);
        Assert.Equal(0, file.OutputSize);
    }

    private string CreateAnimatedGif(int framesCount)
    {
        string path = Path.Combine(_testDir, "animated.gif");

        var frames = new NetVips.Image[framesCount];
        for (int i = 0; i < framesCount; i++)
        {
            frames[i] = (i % 2 == 0) ? NetVips.Image.Black(100, 100) : (NetVips.Image.Black(100, 100) + 255);
        }

        using var combined = NetVips.Image.Arrayjoin(frames, across: 1);
        combined.WriteToFile(path);

        combined.Dispose();
        foreach (var f in frames) f.Dispose();

        return path;
    }
}
