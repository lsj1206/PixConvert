using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Extensions.Logging.Abstractions;
using SkiaSharp;
using PixConvert.Models;
using PixConvert.Services;
using PixConvert.Services.Providers;

namespace PixConvert.Tests;

public class SkiaSharpProviderTests : IDisposable
{
    private readonly SkiaSharpProvider _provider;
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

    public SkiaSharpProviderTests()
    {
        _lang = new MockLanguageService();
        _provider = new SkiaSharpProvider(_lang, NullLogger<SkiaSharpProvider>.Instance);
        _testDir = Path.Combine(Path.GetTempPath(), "PixConvertTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
        _inputPath = Path.Combine(_testDir, "input.png");

        using var bitmap = new SKBitmap(100, 100);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            using var paint = new SKPaint { Color = SKColors.Red };
            canvas.DrawRect(10, 10, 80, 80, paint);
        }
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.OpenWrite(_inputPath);
        data.SaveTo(stream);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Theory]
    [InlineData("JPEG", "jpg")]
    [InlineData("PNG", "png")]
    [InlineData("WEBP", "webp")]
    [InlineData("BMP", "bmp")]
    public async Task ConvertAsync_ShouldConvertFileToVariousFormats(string format, string expectedExt)
    {
        var file = new FileItem { Path = _inputPath, FileSignature = "PNG" };
        var settings = new ConvertSettings
        {
            StandardTargetFormat = format,
            SaveLocation = SaveLocationType.SameAsOriginal,
            FolderMethod = SaveFolderMethod.NoFolder,
            OverwritePolicy = OverwritePolicy.Overwrite
        };

        await _provider.ConvertAsync(file, settings, new ConversionSession(), CancellationToken.None);

        string expectedPath = Path.Combine(_testDir, "input." + expectedExt);
        Assert.True(File.Exists(expectedPath));
        Assert.Equal(FileConvertStatus.Success, file.Status);
    }

    [Fact]
    public async Task ConvertAsync_WhenOverwritePolicyIsSuffix_ShouldGenerateNewFileName()
    {
        var file = new FileItem { Path = _inputPath, FileSignature = "PNG" };
        var settings = new ConvertSettings
        {
            StandardTargetFormat = "JPEG",
            SaveLocation = SaveLocationType.SameAsOriginal,
            FolderMethod = SaveFolderMethod.NoFolder,
            OverwritePolicy = OverwritePolicy.Suffix
        };

        string basePath = Path.Combine(_testDir, "input.jpg");
        File.WriteAllText(basePath, "existing");

        await _provider.ConvertAsync(file, settings, new ConversionSession(), CancellationToken.None);

        string suffixedPath = Path.Combine(_testDir, "input_1.jpg");
        Assert.True(File.Exists(suffixedPath));
        Assert.True(File.Exists(basePath));
    }

    [Fact]
    public async Task ConvertAsync_WhenOverwritePolicyIsSkip_ShouldNotConvert()
    {
        var file = new FileItem { Path = _inputPath, FileSignature = "PNG" };
        var settings = new ConvertSettings
        {
            StandardTargetFormat = "JPEG",
            SaveLocation = SaveLocationType.SameAsOriginal,
            FolderMethod = SaveFolderMethod.NoFolder,
            OverwritePolicy = OverwritePolicy.Skip
        };

        string basePath = Path.Combine(_testDir, "input.jpg");
        File.WriteAllText(basePath, "existing");
        var lastWriteTime = File.GetLastWriteTime(basePath);

        await _provider.ConvertAsync(file, settings, new ConversionSession(), CancellationToken.None);

        Assert.Equal(lastWriteTime, File.GetLastWriteTime(basePath));
        Assert.Equal(FileConvertStatus.Skipped, file.Status);
    }

    [Fact]
    public async Task ConvertAsync_WhenTargetIsJpeg_ShouldCompositeBackground()
    {
        var file = new FileItem { Path = _inputPath, FileSignature = "PNG" };
        var settings = new ConvertSettings
        {
            StandardTargetFormat = "JPEG",
            StandardBackgroundColor = "#000000",
            SaveLocation = SaveLocationType.SameAsOriginal,
            FolderMethod = SaveFolderMethod.NoFolder
        };

        await _provider.ConvertAsync(file, settings, new ConversionSession(), CancellationToken.None);

        string outputPath = Path.Combine(_testDir, "input.jpg");
        using var stream = File.OpenRead(outputPath);
        using var bitmap = SKBitmap.Decode(stream);

        var pixel = bitmap.GetPixel(0, 0);
        Assert.True(pixel.Red < 10);
        Assert.True(pixel.Green < 10);
        Assert.True(pixel.Blue < 10);
        Assert.Equal(255, pixel.Alpha);
    }

    [Fact]
    public async Task ConvertAsync_WhenTargetSupportsAlpha_ShouldPreserveTransparency()
    {
        var file = new FileItem { Path = _inputPath, FileSignature = "PNG" };
        var settings = new ConvertSettings
        {
            StandardTargetFormat = "WEBP",
            SaveLocation = SaveLocationType.SameAsOriginal,
            FolderMethod = SaveFolderMethod.NoFolder
        };

        await _provider.ConvertAsync(file, settings, new ConversionSession(), CancellationToken.None);

        string outputPath = Path.Combine(_testDir, "input.webp");
        using var stream = File.OpenRead(outputPath);
        using var bitmap = SKBitmap.Decode(stream);

        var pixel = bitmap.GetPixel(0, 0);
        Assert.True(pixel.Alpha < 255);
    }

    [Fact]
    public async Task ConvertAsync_WhenJpegSubsamplingIs444_ShouldConvertSuccessfully()
    {
        var file = new FileItem { Path = _inputPath, FileSignature = "PNG" };
        var settings = new ConvertSettings
        {
            StandardTargetFormat = "JPEG",
            StandardQuality = 95,
            StandardJpegChromaSubsampling = JpegChromaSubsamplingMode.Chroma444,
            SaveLocation = SaveLocationType.SameAsOriginal,
            FolderMethod = SaveFolderMethod.NoFolder
        };

        await _provider.ConvertAsync(file, settings, new ConversionSession(), CancellationToken.None);

        string outputPath = Path.Combine(_testDir, "input.jpg");
        Assert.True(File.Exists(outputPath));
        Assert.Equal(FileConvertStatus.Success, file.Status);
    }

    [Fact]
    public async Task ConvertAsync_WhenJpegSubsamplingIs422_ShouldConvertSuccessfully()
    {
        var file = new FileItem { Path = _inputPath, FileSignature = "PNG" };
        var settings = new ConvertSettings
        {
            StandardTargetFormat = "JPEG",
            StandardQuality = 95,
            StandardJpegChromaSubsampling = JpegChromaSubsamplingMode.Chroma422,
            SaveLocation = SaveLocationType.SameAsOriginal,
            FolderMethod = SaveFolderMethod.NoFolder
        };

        await _provider.ConvertAsync(file, settings, new ConversionSession(), CancellationToken.None);

        string outputPath = Path.Combine(_testDir, "input.jpg");
        Assert.True(File.Exists(outputPath));
        Assert.Equal(FileConvertStatus.Success, file.Status);
    }

    [Fact]
    public async Task ConvertAsync_WhenPngCompressionIsCustomized_ShouldConvertSuccessfully()
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

        await _provider.ConvertAsync(file, settings, new ConversionSession(), CancellationToken.None);

        string outputPath = Path.Combine(_testDir, "input_1.png");
        Assert.True(File.Exists(outputPath));
        Assert.Equal(FileConvertStatus.Success, file.Status);
    }

    [Fact]
    public async Task ConvertAsync_WhenCancelled_ShouldThrowAndCleanup()
    {
        var file = new FileItem { Path = _inputPath, FileSignature = "PNG" };
        var settings = new ConvertSettings { StandardTargetFormat = "JPEG" };
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await _provider.ConvertAsync(file, settings, new ConversionSession(), cts.Token));
    }

    [Fact]
    public async Task ConvertAsync_WhenFileIsCorrupted_ShouldSetErrorStatus()
    {
        string corruptedPath = Path.Combine(_testDir, "corrupted.png");
        File.WriteAllText(corruptedPath, "not an image");
        var file = new FileItem { Path = corruptedPath, FileSignature = "PNG" };
        var settings = new ConvertSettings { StandardTargetFormat = "JPEG" };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _provider.ConvertAsync(file, settings, new ConversionSession(), CancellationToken.None));

        Assert.Contains("SKBitmap.Decode returned null", ex.Message);
        Assert.Equal(FileConvertStatus.Error, file.Status);
    }

    [Fact]
    public async Task ConvertAsync_WithCustomHexColor_ShouldApplyCorrectly()
    {
        var file = new FileItem { Path = _inputPath, FileSignature = "PNG" };
        var settings = new ConvertSettings
        {
            StandardTargetFormat = "JPEG",
            StandardBackgroundColor = "#FF00FF",
            SaveLocation = SaveLocationType.SameAsOriginal,
            FolderMethod = SaveFolderMethod.NoFolder
        };

        await _provider.ConvertAsync(file, settings, new ConversionSession(), CancellationToken.None);

        string outputPath = Path.Combine(_testDir, "input.jpg");
        using var stream = File.OpenRead(outputPath);
        using var bitmap = SKBitmap.Decode(stream);

        var pixel = bitmap.GetPixel(0, 0);
        Assert.True(pixel.Red > 245);
        Assert.True(pixel.Green < 10);
        Assert.True(pixel.Blue > 245);
    }

    [Fact]
    public async Task ConvertAsync_WhenOutputTypeIsSubFolder_ShouldCreateSubFolderAndSaveFile()
    {
        var file = new FileItem { Path = _inputPath, FileSignature = "PNG" };
        var settings = new ConvertSettings
        {
            StandardTargetFormat = "JPEG",
            SaveLocation = SaveLocationType.SameAsOriginal,
            FolderMethod = SaveFolderMethod.CreateFolder,
            OutputSubFolderName = $"PixConvert_{DateTime.Today:yyyy-MM-dd}",
            OverwritePolicy = OverwritePolicy.Overwrite
        };

        await _provider.ConvertAsync(file, settings, new ConversionSession(), CancellationToken.None);

        string dateStr = DateTime.Today.ToString("yyyy-MM-dd");
        string expectedDir = Path.Combine(_testDir, $"PixConvert_{dateStr}");
        string expectedPath = Path.Combine(expectedDir, "input.jpg");

        Assert.True(Directory.Exists(expectedDir));
        Assert.True(File.Exists(expectedPath));
        Assert.Equal(FileConvertStatus.Success, file.Status);
    }

    [Fact]
    public async Task ConvertAsync_WhenTargetIsBmp_ShouldCompositeBackground()
    {
        var file = new FileItem { Path = _inputPath, FileSignature = "PNG" };
        var settings = new ConvertSettings
        {
            StandardTargetFormat = "BMP",
            StandardBackgroundColor = "#0000FF",
            SaveLocation = SaveLocationType.SameAsOriginal,
            FolderMethod = SaveFolderMethod.NoFolder
        };

        await _provider.ConvertAsync(file, settings, new ConversionSession(), CancellationToken.None);

        string outputPath = Path.Combine(_testDir, "input.bmp");
        Assert.True(File.Exists(outputPath));
        using var stream = File.OpenRead(outputPath);
        using var bitmap = SKBitmap.Decode(stream);

        var pixel = bitmap.GetPixel(0, 0);
        Assert.Equal(0, pixel.Red);
        Assert.Equal(0, pixel.Green);
        Assert.Equal(255, pixel.Blue);
        Assert.Equal(255, pixel.Alpha);
    }
}
