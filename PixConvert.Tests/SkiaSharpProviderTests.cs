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

        // 투명도가 있는 테스트용 PNG 생성 (Red square with transparent background)
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
        // Arrange
        var file = new FileItem { Path = _inputPath, FileSignature = "PNG" };
        var settings = new ConvertSettings
        {
            StandardTargetFormat = format,
            OutputLocation = OutputLocationType.SameAsOriginal,
            FolderStrategy = OutputFolderStrategy.NoFolder,
            OverwriteSide = OverwritePolicy.Overwrite
        };

        // Act
        await _provider.ConvertAsync(file, settings, new ConversionSession(), CancellationToken.None);

        // Assert
        string expectedPath = Path.Combine(_testDir, "input." + expectedExt);
        Assert.True(File.Exists(expectedPath));
        Assert.Equal(FileConvertStatus.Success, file.Status);
    }

    [Fact]
    public async Task ConvertAsync_WhenOverwritePolicyIsSuffix_ShouldGenerateNewFileName()
    {
        // Arrange
        var file = new FileItem { Path = _inputPath, FileSignature = "PNG" };
        var settings = new ConvertSettings
        {
            StandardTargetFormat = "JPEG",
            OutputLocation = OutputLocationType.SameAsOriginal,
            FolderStrategy = OutputFolderStrategy.NoFolder,
            OverwriteSide = OverwritePolicy.Suffix
        };

        // 이미 파일이 존재하는 상황 시뮬레이션
        string basePath = Path.Combine(_testDir, "input.jpg");
        File.WriteAllText(basePath, "existing");

        // Act
        await _provider.ConvertAsync(file, settings, new ConversionSession(), CancellationToken.None);

        // Assert
        string suffixedPath = Path.Combine(_testDir, "input_1.jpg");
        Assert.True(File.Exists(suffixedPath));
        Assert.True(File.Exists(basePath));
    }

    [Fact]
    public async Task ConvertAsync_WhenOverwritePolicyIsSkip_ShouldNotConvert()
    {
        // Arrange
        var file = new FileItem { Path = _inputPath, FileSignature = "PNG" };
        var settings = new ConvertSettings
        {
            StandardTargetFormat = "JPEG",
            OutputLocation = OutputLocationType.SameAsOriginal,
            FolderStrategy = OutputFolderStrategy.NoFolder,
            OverwriteSide = OverwritePolicy.Skip
        };

        string basePath = Path.Combine(_testDir, "input.jpg");
        File.WriteAllText(basePath, "existing");
        var lastWriteTime = File.GetLastWriteTime(basePath);

        // Act
        await _provider.ConvertAsync(file, settings, new ConversionSession(), CancellationToken.None);

        // Assert
        Assert.Equal(lastWriteTime, File.GetLastWriteTime(basePath));
        Assert.Equal(FileConvertStatus.Skipped, file.Status);
    }

    [Fact]
    public async Task ConvertAsync_WhenTargetIsJpeg_ShouldCompositeBackground()
    {
        // Arrange
        var file = new FileItem { Path = _inputPath, FileSignature = "PNG" };
        var settings = new ConvertSettings
        {
            StandardTargetFormat = "JPEG",
            BgColorOption = BackgroundColorOption.Black, // 검은색 배경 합성
            OutputLocation = OutputLocationType.SameAsOriginal,
            FolderStrategy = OutputFolderStrategy.NoFolder
        };

        // Act
        await _provider.ConvertAsync(file, settings, new ConversionSession(), CancellationToken.None);

        // Assert
        string outputPath = Path.Combine(_testDir, "input.jpg");
        using var stream = File.OpenRead(outputPath);
        using var bitmap = SKBitmap.Decode(stream);

        // 투명했던 (0,0) 좌표가 검은색(0,0,0)에 근사해야 함 (JPEG 손실 압축 고려)
        var pixel = bitmap.GetPixel(0, 0);
        Assert.True(pixel.Red < 10);
        Assert.True(pixel.Green < 10);
        Assert.True(pixel.Blue < 10);
        Assert.Equal(255, pixel.Alpha);
    }

    [Fact]
    public async Task ConvertAsync_WhenTargetSupportsAlpha_ShouldPreserveTransparency()
    {
        // Arrange
        var file = new FileItem { Path = _inputPath, FileSignature = "PNG" };
        var settings = new ConvertSettings
        {
            StandardTargetFormat = "WEBP",
            OutputLocation = OutputLocationType.SameAsOriginal,
            FolderStrategy = OutputFolderStrategy.NoFolder
        };

        // Act
        await _provider.ConvertAsync(file, settings, new ConversionSession(), CancellationToken.None);

        // Assert
        string outputPath = Path.Combine(_testDir, "input.webp");
        using var stream = File.OpenRead(outputPath);
        using var bitmap = SKBitmap.Decode(stream);

        // 투명도가 유지되어야 함 (Alpha < 255)
        var pixel = bitmap.GetPixel(0, 0);
        Assert.True(pixel.Alpha < 255);
    }

    [Fact]
    public async Task ConvertAsync_WhenCancelled_ShouldThrowAndCleanup()
    {
        // Arrange
        var file = new FileItem { Path = _inputPath, FileSignature = "PNG" };
        var settings = new ConvertSettings { StandardTargetFormat = "JPEG" };
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await _provider.ConvertAsync(file, settings, new ConversionSession(), cts.Token));
    }

    [Fact]
    public async Task ConvertAsync_WhenFileIsCorrupted_ShouldSetErrorStatus()
    {
        // Arrange
        string corruptedPath = Path.Combine(_testDir, "corrupted.png");
        File.WriteAllText(corruptedPath, "not an image");
        var file = new FileItem { Path = corruptedPath, FileSignature = "PNG" };
        var settings = new ConvertSettings { StandardTargetFormat = "JPEG" };

        // Act & Assert - 래핑 없이 원본 예외(InvalidOperationException)가 전파됨
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _provider.ConvertAsync(file, settings, new ConversionSession(), CancellationToken.None));

        Assert.Contains("SKBitmap.Decode returned null", ex.Message);
        Assert.Equal(FileConvertStatus.Error, file.Status);
    }

    [Fact]
    public async Task ConvertAsync_WithCustomHexColor_ShouldApplyCorrectly()
    {
        // Arrange
        var file = new FileItem { Path = _inputPath, FileSignature = "PNG" };
        var settings = new ConvertSettings
        {
            StandardTargetFormat = "JPEG",
            BgColorOption = BackgroundColorOption.Custom,
            CustomBackgroundColor = "#FF00FF", // Magenta
            OutputLocation = OutputLocationType.SameAsOriginal,
            FolderStrategy = OutputFolderStrategy.NoFolder
        };

        // Act
        await _provider.ConvertAsync(file, settings, new ConversionSession(), CancellationToken.None);

        // Assert
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
        // Arrange
        var file = new FileItem { Path = _inputPath, FileSignature = "PNG" };
        var settings = new ConvertSettings
        {
            StandardTargetFormat = "JPEG",
            OutputLocation = OutputLocationType.SameAsOriginal,
            FolderStrategy = OutputFolderStrategy.CreateFolder,
            OutputSubFolderName = $"PixConvert_{DateTime.Today:yyyy-MM-dd}",
            OverwriteSide = OverwritePolicy.Overwrite
        };

        // Act
        await _provider.ConvertAsync(file, settings, new ConversionSession(), CancellationToken.None);

        // Assert
        string dateStr = DateTime.Today.ToString("yyyy-MM-dd");
        string expectedDir = Path.Combine(_testDir, $"PixConvert_{dateStr}");
        string expectedPath = Path.Combine(expectedDir, "input.jpg");

        Assert.True(Directory.Exists(expectedDir));
        Assert.True(File.Exists(expectedPath));
        Assert.True(File.Exists(expectedPath));
        Assert.Equal(FileConvertStatus.Success, file.Status);
    }

    [Fact]
    public async Task ConvertAsync_WhenTargetIsBmp_ShouldCompositeBackground()
    {
        // Arrange
        var file = new FileItem { Path = _inputPath, FileSignature = "PNG" };
        var settings = new ConvertSettings
        {
            StandardTargetFormat = "BMP",
            BgColorOption = BackgroundColorOption.Custom,
            CustomBackgroundColor = "#0000FF", // 파란색 배경 합성
            OutputLocation = OutputLocationType.SameAsOriginal,
            FolderStrategy = OutputFolderStrategy.NoFolder
        };

        // Act
        await _provider.ConvertAsync(file, settings, new ConversionSession(), CancellationToken.None);

        // Assert
        string outputPath = Path.Combine(_testDir, "input.bmp");
        Assert.True(File.Exists(outputPath));
        using var stream = File.OpenRead(outputPath);
        using var bitmap = SKBitmap.Decode(stream);

        // 투명했던 (0,0) 좌표가 파란색(0,0,255)이어야 함
        var pixel = bitmap.GetPixel(0, 0);
        Assert.Equal(0, pixel.Red);
        Assert.Equal(0, pixel.Green);
        Assert.Equal(255, pixel.Blue);
        Assert.Equal(255, pixel.Alpha);
    }
}
