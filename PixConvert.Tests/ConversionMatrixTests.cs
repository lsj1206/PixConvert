using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NetVips;
using SkiaSharp;
using Xunit;
using PixConvert.Models;
using PixConvert.Services;
using PixConvert.Services.Interfaces;
using PixConvert.Services.Providers;

namespace PixConvert.Tests;

public class ConversionMatrixTests : IDisposable
{
    private const int Width = 48;
    private const int Height = 36;
    private const int AnimationFrames = 3;

    private readonly string _testDir;
    private readonly SkiaSharpProvider _skiaSharp;
    private readonly NetVipsProvider _netVips;
    private readonly EngineSelector _selector;

    private sealed class MockLanguageService : ILanguageService
    {
        public string GetString(string key) => key;
        public void ChangeLanguage(string culture) { }
        public string GetSystemLanguage() => "ko-KR";
        public string GetCurrentLanguage() => "ko-KR";
        public event Action LanguageChanged = delegate { };
    }

    public ConversionMatrixTests()
    {
        var languageService = new MockLanguageService();
        _testDir = Path.Combine(Path.GetTempPath(), "PixConvertMatrix_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);

        _skiaSharp = new SkiaSharpProvider(languageService, NullLogger<SkiaSharpProvider>.Instance);
        _netVips = new NetVipsProvider(languageService, NullLogger<NetVipsProvider>.Instance);
        _selector = new EngineSelector(_skiaSharp, _netVips);
    }

    public static IEnumerable<object[]> StaticConversionCases()
    {
        string[] sourceFormats = { "JPEG", "PNG", "BMP", "WEBP", "AVIF" };
        string[] targetFormats = { "JPEG", "PNG", "BMP", "WEBP", "AVIF" };

        foreach (string sourceFormat in sourceFormats)
        {
            foreach (string targetFormat in targetFormats)
            {
                yield return new object[] { sourceFormat, targetFormat };
            }
        }
    }

    public static IEnumerable<object[]> AnimatedConversionCases()
    {
        string[] sourceFormats = { "GIF", "WEBP" };
        string[] targetFormats = { "GIF", "WEBP" };

        foreach (string sourceFormat in sourceFormats)
        {
            foreach (string targetFormat in targetFormats)
            {
                yield return new object[] { sourceFormat, targetFormat };
            }
        }
    }

    [Theory]
    [MemberData(nameof(StaticConversionCases))]
    public async Task StaticConversionMatrix_ShouldConvertEverySupportedCombination(string sourceFormat, string targetFormat)
    {
        FileItem file = await CreateStaticInputAsync(sourceFormat);
        var settings = CreateStaticSettings(targetFormat);

        var provider = _selector.GetProvider(file, settings);
        Assert.Equal(ExpectedStaticProviderName(sourceFormat, targetFormat), provider.Name);

        var result = await provider.ConvertAsync(file, settings, new ConversionSession(), CancellationToken.None);

        AssertSuccessfulOutput(result, targetFormat);
        AssertImageCanReopen(result.OutputPath!);
    }

    [Theory]
    [MemberData(nameof(AnimatedConversionCases))]
    public async Task AnimatedConversionMatrix_ShouldConvertEverySupportedCombinationAndKeepMultipleFrames(
        string sourceFormat,
        string targetFormat)
    {
        FileItem file = CreateAnimatedInput(sourceFormat);
        var settings = CreateAnimationSettings(targetFormat);

        var provider = _selector.GetProvider(file, settings);
        Assert.Equal("NetVips", provider.Name);

        var result = await provider.ConvertAsync(file, settings, new ConversionSession(), CancellationToken.None);

        AssertSuccessfulOutput(result, targetFormat);
        AssertImageCanReopen(result.OutputPath!);
        Assert.True(GetLoadedFrameCount(result.OutputPath!) >= 2, $"{sourceFormat} -> {targetFormat} collapsed to a single frame.");
    }

    public void Dispose()
    {
        _netVips.Dispose();
        _skiaSharp.Dispose();

        GC.Collect();
        GC.WaitForPendingFinalizers();

        if (!Directory.Exists(_testDir))
            return;

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

    private async Task<FileItem> CreateStaticInputAsync(string sourceFormat)
    {
        string path = Path.Combine(_testDir, "source." + GetExtension(sourceFormat));

        switch (sourceFormat)
        {
            case "JPEG":
                SaveSkiaRaster(path, SKEncodedImageFormat.Jpeg, hasAlpha: false);
                break;
            case "PNG":
                SaveSkiaRaster(path, SKEncodedImageFormat.Png, hasAlpha: true);
                break;
            case "BMP":
                using (var bitmap = CreateRasterBitmap(hasAlpha: false))
                {
                    await BmpEncoder.SaveAsync(bitmap, path);
                }
                break;
            case "WEBP":
                SaveSkiaRaster(path, SKEncodedImageFormat.Webp, hasAlpha: true);
                break;
            case "AVIF":
                SaveAvifRaster(path);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(sourceFormat), sourceFormat, null);
        }

        return new FileItem
        {
            Path = path,
            Size = new FileInfo(path).Length,
            FileSignature = sourceFormat,
            IsAnimation = false
        };
    }

    private FileItem CreateAnimatedInput(string sourceFormat)
    {
        string path = Path.Combine(_testDir, "animated." + GetExtension(sourceFormat));

        using (var image = CreateAnimatedVipsImage())
        {
            if (sourceFormat == "GIF")
            {
                image.Gifsave(path, keep: Enums.ForeignKeep.None);
            }
            else if (sourceFormat == "WEBP")
            {
                image.Webpsave(path, q: 80, lossless: false, keep: Enums.ForeignKeep.None);
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(sourceFormat), sourceFormat, null);
            }
        }

        return new FileItem
        {
            Path = path,
            Size = new FileInfo(path).Length,
            FileSignature = sourceFormat,
            IsAnimation = true
        };
    }

    private static ConvertSettings CreateStaticSettings(string targetFormat) =>
        new()
        {
            StandardTargetFormat = targetFormat,
            StandardQuality = 82,
            StandardLossless = false,
            StandardJpegChromaSubsampling = JpegChromaSubsamplingMode.Chroma444,
            StandardPngCompressionLevel = 6,
            StandardAvifChromaSubsampling = AvifChromaSubsamplingMode.Auto,
            StandardAvifEncodingEffort = 4,
            StandardAvifBitDepth = AvifBitDepthMode.Bit8,
            StandardBackgroundColor = "#FFFFFF",
            SaveLocation = SaveLocationType.SameAsOriginal,
            FolderMethod = SaveFolderMethod.NoFolder,
            OverwritePolicy = OverwritePolicy.Suffix
        };

    private static ConvertSettings CreateAnimationSettings(string targetFormat) =>
        new()
        {
            AnimationTargetFormat = targetFormat,
            AnimationQuality = 80,
            AnimationLossless = false,
            AnimationWebpEncodingEffort = 4,
            AnimationWebpPreset = WebpPresetMode.Default,
            AnimationGifPalettePreset = GifPalettePreset.Standard,
            AnimationGifInterframeMaxError = 0.0,
            AnimationGifInterpaletteMaxError = 0.0,
            SaveLocation = SaveLocationType.SameAsOriginal,
            FolderMethod = SaveFolderMethod.NoFolder,
            OverwritePolicy = OverwritePolicy.Suffix
        };

    private static string ExpectedStaticProviderName(string sourceFormat, string targetFormat) =>
        sourceFormat == "AVIF" || targetFormat == "AVIF"
            ? "NetVips"
            : "SkiaSharp";

    private static void AssertSuccessfulOutput(ConversionResult result, string targetFormat)
    {
        Assert.Equal(FileConvertStatus.Success, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.OutputPath));
        Assert.True(File.Exists(result.OutputPath), $"Missing output file: {result.OutputPath}");
        Assert.True(result.OutputSize > 0, $"Output file is empty: {result.OutputPath}");
        Assert.Equal("." + GetExtension(targetFormat), Path.GetExtension(result.OutputPath));
    }

    private static void AssertImageCanReopen(string outputPath)
    {
        try
        {
            using var image = Image.NewFromFile(outputPath);
            Assert.True(image.Width > 0);
            Assert.True(image.Height > 0);
            return;
        }
        catch (Exception netVipsException)
        {
            using var stream = File.OpenRead(outputPath);
            using var bitmap = SKBitmap.Decode(stream);

            Assert.NotNull(bitmap);
            Assert.True(bitmap!.Width > 0, netVipsException.Message);
            Assert.True(bitmap.Height > 0, netVipsException.Message);
        }
    }

    private static int GetLoadedFrameCount(string path)
    {
        var loaderOptions = new VOption();
        loaderOptions.Add("n", -1);

        using var image = Image.NewFromFile(path, kwargs: loaderOptions);
        int pageCount = TryGetIntMetadata(image, "n-pages");
        if (pageCount > 0)
            return pageCount;

        int pageHeight = TryGetIntMetadata(image, "page-height");
        return pageHeight > 0
            ? Math.Max(1, image.Height / pageHeight)
            : 1;
    }

    private static int TryGetIntMetadata(Image image, string key)
    {
        try
        {
            object value = image.Get(key);
            if (value is int intValue)
                return intValue;
            if (value is uint uintValue)
                return checked((int)uintValue);
            if (value is long longValue)
                return checked((int)longValue);
            if (value is double doubleValue)
                return (int)doubleValue;

            string? text = Convert.ToString(value, CultureInfo.InvariantCulture);
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                ? parsed
                : 0;
        }
        catch
        {
            return 0;
        }
    }

    private static void SaveSkiaRaster(string path, SKEncodedImageFormat format, bool hasAlpha)
    {
        using var bitmap = CreateRasterBitmap(hasAlpha);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 90);
        Assert.NotNull(data);

        using var stream = File.OpenWrite(path);
        data!.SaveTo(stream);
    }

    private static void SaveAvifRaster(string path)
    {
        string seedPath = Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path) + "_seed.png");
        SaveSkiaRaster(seedPath, SKEncodedImageFormat.Png, hasAlpha: true);

        using var image = Image.NewFromFile(seedPath);
        image.Heifsave(
            path,
            q: 82,
            compression: Enums.ForeignHeifCompression.Av1,
            bitdepth: 8,
            keep: Enums.ForeignKeep.None);
    }

    private static SKBitmap CreateRasterBitmap(bool hasAlpha)
    {
        var alphaType = hasAlpha ? SKAlphaType.Premul : SKAlphaType.Opaque;
        var bitmap = new SKBitmap(new SKImageInfo(Width, Height, SKImageInfo.PlatformColorType, alphaType));

        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(hasAlpha ? SKColors.Transparent : SKColors.White);

        using var redPaint = new SKPaint { Color = SKColors.Red, IsAntialias = false };
        using var bluePaint = new SKPaint { Color = SKColors.Blue, IsAntialias = false };
        canvas.DrawRect(4, 4, 24, 20, redPaint);
        canvas.DrawRect(20, 12, 20, 18, bluePaint);
        canvas.Flush();

        return bitmap;
    }

    private static Image CreateAnimatedVipsImage()
    {
        var frames = new Image[AnimationFrames];

        try
        {
            for (int i = 0; i < AnimationFrames; i++)
            {
                frames[i] = Image.Black(Width, Height) + (i * 80);
            }

            using var joined = Image.Arrayjoin(frames, across: 1);
            return joined.Mutate(mutable =>
            {
                mutable.Set(GValue.GIntType, "page-height", Height);
                mutable.Set(GValue.ArrayIntType, "delay", Enumerable.Repeat(80, AnimationFrames).ToArray());
                mutable.Set(GValue.GIntType, "loop", 0);
            });
        }
        finally
        {
            foreach (var frame in frames)
            {
                frame?.Dispose();
            }
        }
    }

    private static string GetExtension(string format) =>
        format switch
        {
            "JPEG" => "jpg",
            "PNG" => "png",
            "BMP" => "bmp",
            "WEBP" => "webp",
            "AVIF" => "avif",
            "GIF" => "gif",
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        };
}
