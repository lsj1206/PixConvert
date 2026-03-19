using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using PixConvert.Models;
using PixConvert.Services.Providers;

namespace PixConvert.Tests;

public class NetVipsProviderTests : IDisposable
{
    private readonly NetVipsProvider _provider;
    private readonly string _testDir;
    private readonly string _inputPath;

    public NetVipsProviderTests()
    {
        _provider = new NetVipsProvider();
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
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public async Task ConvertAsync_WhenTargetIsAvif_ShouldWork()
    {
        // Arrange
        var file = new FileItem { Path = _inputPath, FileSignature = "PNG" };
        var settings = new ConvertSettings 
        { 
            StandardTargetFormat = "AVIF", 
            OutputType = OutputPathType.SameFolder 
        };

        // Act
        await _provider.ConvertAsync(file, settings, CancellationToken.None);

        // Assert
        string expectedPath = Path.Combine(_testDir, "input.avif");
        Assert.True(File.Exists(expectedPath));
        Assert.Equal(FileConvertStatus.Success, file.Status);
    }
}
