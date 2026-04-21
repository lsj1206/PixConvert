using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using PixConvert.Models;
using PixConvert.Services;
using PixConvert.Services.Providers;
using SkiaSharp;
using Xunit;

namespace PixConvert.Tests;

public class SkiaSharpProviderTests : IDisposable
{
    private readonly TempDirectoryFixture _tempDirectory = new("PixConvertTests_");
    private readonly SkiaSharpProvider _provider;
    private readonly string _inputPath;

    public SkiaSharpProviderTests()
    {
        _provider = new SkiaSharpProvider(new FakeLanguageService(), NullLogger<SkiaSharpProvider>.Instance);
        _inputPath = _tempDirectory.CreatePath("input.png");
        TestImageFactory.CreateTransparentPng(_inputPath);
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

        string basePath = _tempDirectory.CreatePath("input.jpg");
        File.WriteAllText(basePath, "existing");

        string suffixedPath = _tempDirectory.CreatePath("input_1.jpg");
        var result = await _provider.ConvertAsync(file, settings, new ConversionSession(), CancellationToken.None);

        ProviderTestAssertions.AssertSuccessResult(result, suffixedPath);
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

        string basePath = _tempDirectory.CreatePath("input.jpg");
        File.WriteAllText(basePath, "existing");
        var lastWriteTime = File.GetLastWriteTime(basePath);

        var result = await _provider.ConvertAsync(file, settings, new ConversionSession(), CancellationToken.None);

        Assert.Equal(lastWriteTime, File.GetLastWriteTime(basePath));
        Assert.Equal(FileConvertStatus.Skipped, result.Status);
        Assert.Null(result.OutputPath);
        ProviderTestAssertions.AssertProviderDidNotMutateFileItem(file);
    }

    [Fact]
    public async Task ConvertAsync_WhenOverwriteBatchOutputCollides_ShouldKeepAllOutputsWithSuffix()
    {
        string inputDirA = _tempDirectory.EnsureDirectory("A");
        string inputDirB = _tempDirectory.EnsureDirectory("B");
        string outputDir = _tempDirectory.EnsureDirectory("Out");

        string firstInput = Path.Combine(inputDirA, "photo.png");
        string secondInput = Path.Combine(inputDirB, "photo.png");
        File.Copy(_inputPath, firstInput);
        File.Copy(_inputPath, secondInput);

        string baseOutput = Path.Combine(outputDir, "photo.jpg");
        string suffixedOutput = Path.Combine(outputDir, "photo_1.jpg");
        File.WriteAllText(baseOutput, "existing");

        var first = new FileItem { Path = firstInput, FileSignature = "PNG" };
        var second = new FileItem { Path = secondInput, FileSignature = "PNG" };
        var settings = new ConvertSettings
        {
            StandardTargetFormat = "JPEG",
            SaveLocation = SaveLocationType.Custom,
            CustomOutputPath = outputDir,
            FolderMethod = SaveFolderMethod.NoFolder,
            OverwritePolicy = OverwritePolicy.Overwrite
        };

        using var session = new ConversionSession();
        var results = await Task.WhenAll(
            _provider.ConvertAsync(first, settings, session, CancellationToken.None),
            _provider.ConvertAsync(second, settings, session, CancellationToken.None));

        var outputPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            results[0].OutputPath!,
            results[1].OutputPath!
        };

        Assert.All(results, result => Assert.Equal(FileConvertStatus.Success, result.Status));
        Assert.Equal(2, outputPaths.Count);
        Assert.Contains(baseOutput, outputPaths);
        Assert.Contains(suffixedOutput, outputPaths);

        using var baseStream = File.OpenRead(baseOutput);
        using var baseBitmap = SKBitmap.Decode(baseStream);
        using var suffixStream = File.OpenRead(suffixedOutput);
        using var suffixBitmap = SKBitmap.Decode(suffixStream);
        Assert.NotNull(baseBitmap);
        Assert.NotNull(suffixBitmap);
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

        string outputPath = _tempDirectory.CreatePath("input.jpg");
        var result = await _provider.ConvertAsync(file, settings, new ConversionSession(), CancellationToken.None);
        ProviderTestAssertions.AssertSuccessResult(result, outputPath);

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

        string outputPath = _tempDirectory.CreatePath("input.webp");
        var result = await _provider.ConvertAsync(file, settings, new ConversionSession(), CancellationToken.None);
        ProviderTestAssertions.AssertSuccessResult(result, outputPath);

        using var stream = File.OpenRead(outputPath);
        using var bitmap = SKBitmap.Decode(stream);

        var pixel = bitmap.GetPixel(0, 0);
        Assert.True(pixel.Alpha < 255);
    }

    [Fact]
    public async Task ConvertAsync_WhenCancelled_ShouldThrow()
    {
        var file = new FileItem { Path = _inputPath, FileSignature = "PNG" };
        var settings = new ConvertSettings { StandardTargetFormat = "JPEG" };
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await _provider.ConvertAsync(file, settings, new ConversionSession(), cts.Token));
    }

    [Fact]
    public async Task ConvertAsync_WhenFileIsCorrupted_ShouldThrowWithoutMutatingFile()
    {
        string corruptedPath = _tempDirectory.CreatePath("corrupted.png");
        File.WriteAllText(corruptedPath, "not an image");
        var file = new FileItem { Path = corruptedPath, FileSignature = "PNG" };
        var settings = new ConvertSettings { StandardTargetFormat = "JPEG" };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _provider.ConvertAsync(file, settings, new ConversionSession(), CancellationToken.None));

        Assert.Contains("SKBitmap.Decode returned null", ex.Message);
        ProviderTestAssertions.AssertProviderDidNotMutateFileItem(file);
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

        string outputPath = _tempDirectory.CreatePath("input.jpg");
        var result = await _provider.ConvertAsync(file, settings, new ConversionSession(), CancellationToken.None);
        ProviderTestAssertions.AssertSuccessResult(result, outputPath);

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

        string dateStr = DateTime.Today.ToString("yyyy-MM-dd");
        string expectedDir = _tempDirectory.CreatePath($"PixConvert_{dateStr}");
        string expectedPath = Path.Combine(expectedDir, "input.jpg");
        var result = await _provider.ConvertAsync(file, settings, new ConversionSession(), CancellationToken.None);

        Assert.True(Directory.Exists(expectedDir));
        ProviderTestAssertions.AssertSuccessResult(result, expectedPath);
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

        string outputPath = _tempDirectory.CreatePath("input.bmp");
        var result = await _provider.ConvertAsync(file, settings, new ConversionSession(), CancellationToken.None);
        ProviderTestAssertions.AssertSuccessResult(result, outputPath);

        using var stream = File.OpenRead(outputPath);
        using var bitmap = SKBitmap.Decode(stream);

        var pixel = bitmap.GetPixel(0, 0);
        Assert.Equal(0, pixel.Red);
        Assert.Equal(0, pixel.Green);
        Assert.Equal(255, pixel.Blue);
        Assert.Equal(255, pixel.Alpha);
    }

    public void Dispose()
    {
        _tempDirectory.Dispose();
    }
}
