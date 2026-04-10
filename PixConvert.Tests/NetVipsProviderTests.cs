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
            StandardAvifEncodingEffort = AvifEncodingEffortMode.Slow,
            StandardAvifBitDepth = AvifBitDepthMode.Bit8,
            SaveLocation = SaveLocationType.SameAsOriginal,
            FolderMethod = SaveFolderMethod.NoFolder
        };

        await _provider.ConvertAsync(file, settings, new ConversionSession(), CancellationToken.None);

        string expectedPath = Path.Combine(_testDir, "input.avif");
        Assert.True(File.Exists(expectedPath));
        Assert.Equal(FileConvertStatus.Success, file.Status);
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

        await _provider.ConvertAsync(file, settings, new ConversionSession(), CancellationToken.None);

        string expectedPath = Path.Combine(_testDir, "input.jpg");
        Assert.True(File.Exists(expectedPath));
        Assert.Equal(FileConvertStatus.Success, file.Status);
    }

    [Fact]
    public async Task ConvertAsync_WhenTargetIsPngWithCustomCompressionAndFilter_ShouldWork()
    {
        var file = new FileItem { Path = _inputPath, FileSignature = "PNG" };
        var settings = new ConvertSettings
        {
            StandardTargetFormat = "PNG",
            StandardPngCompressionLevel = 9,
            StandardPngFilter = PngFilterMode.Paeth,
            SaveLocation = SaveLocationType.SameAsOriginal,
            FolderMethod = SaveFolderMethod.NoFolder,
            OverwritePolicy = OverwritePolicy.Suffix
        };

        await _provider.ConvertAsync(file, settings, new ConversionSession(), CancellationToken.None);

        string expectedPath = Path.Combine(_testDir, "input_1.png");
        Assert.True(File.Exists(expectedPath));
        Assert.Equal(FileConvertStatus.Success, file.Status);
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

        try
        {
            await _provider.ConvertAsync(file, settings, new ConversionSession(), CancellationToken.None);
        }
        catch (Exception)
        {
        }

        Assert.True(file.Status == FileConvertStatus.Success || file.Status == FileConvertStatus.Error);
        Assert.NotEqual(FileConvertStatus.Pending, file.Status);
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

        await _provider.ConvertAsync(file, settings, new ConversionSession(), CancellationToken.None);

        string expectedPath = Path.Combine(_testDir, "animated.webp");
        Assert.True(File.Exists(expectedPath));
        Assert.Equal(FileConvertStatus.Success, file.Status);

        using (var output = NetVips.Image.NewFromFile(expectedPath))
        {
            Assert.NotNull(output);
        }
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

        await _provider.ConvertAsync(file, settings, new ConversionSession(), CancellationToken.None);

        string expectedPath = Path.Combine(_testDir, "input.jpg");
        Assert.True(File.Exists(expectedPath));

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

        try { await _provider.ConvertAsync(file, settings, new ConversionSession(), CancellationToken.None); }
        catch { }

        Assert.Equal(FileConvertStatus.Error, file.Status);
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
