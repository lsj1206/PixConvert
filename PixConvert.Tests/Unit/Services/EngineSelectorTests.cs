using Microsoft.Extensions.Logging.Abstractions;
using PixConvert.Models;
using PixConvert.Services;
using PixConvert.Services.Providers;
using Xunit;

namespace PixConvert.Tests;

public class EngineSelectorTests
{
    private readonly EngineSelector _selector;

    public EngineSelectorTests()
    {
        var language = new FakeLanguageService();
        var skia = new SkiaSharpProvider(language, NullLogger<SkiaSharpProvider>.Instance);
        var netVips = new NetVipsProvider(language, NullLogger<NetVipsProvider>.Instance);
        _selector = new EngineSelector(skia, netVips);
    }

    [Fact]
    public void GetProvider_WhenStaticJpegInput_ShouldReturnSkiaSharp()
    {
        var file = new FileItem { Path = "test.jpg", FileSignature = "JPEG", IsAnimation = false };

        var provider = _selector.GetProvider(file, new ConvertSettings());

        Assert.IsType<SkiaSharpProvider>(provider);
    }

    [Fact]
    public void GetProvider_WhenStaticWebpInput_ShouldReturnSkiaSharp()
    {
        var file = new FileItem { Path = "test.webp", FileSignature = "WEBP", IsAnimation = false };

        var provider = _selector.GetProvider(file, new ConvertSettings());

        Assert.IsType<SkiaSharpProvider>(provider);
    }

    [Fact]
    public void GetProvider_WhenAnimatedWebpInput_ShouldReturnNetVips()
    {
        var file = new FileItem { Path = "test.webp", FileSignature = "WEBP", IsAnimation = true };

        var provider = _selector.GetProvider(file, new ConvertSettings { AnimationTargetFormat = "GIF" });

        Assert.IsType<NetVipsProvider>(provider);
    }

    [Theory]
    [InlineData("avif")]
    [InlineData("AVIF")]
    public void GetProvider_WhenInputSignatureIsAvif_ShouldReturnNetVips(string signature)
    {
        var file = new FileItem { Path = "test.avif", FileSignature = signature, IsAnimation = false };

        var provider = _selector.GetProvider(file, new ConvertSettings { StandardTargetFormat = "PNG" });

        Assert.IsType<NetVipsProvider>(provider);
    }

    [Fact]
    public void GetProvider_WhenStandardTargetFormatIsAvif_ShouldReturnNetVips()
    {
        var file = new FileItem { Path = "test.png", FileSignature = "PNG", IsAnimation = false };

        var provider = _selector.GetProvider(file, new ConvertSettings { StandardTargetFormat = "AVIF" });

        Assert.IsType<NetVipsProvider>(provider);
    }

    [Fact]
    public void GetProvider_WhenStandardTargetFormatIsBmp_ShouldReturnSkiaSharp()
    {
        var file = new FileItem { Path = "test.png", FileSignature = "PNG", IsAnimation = false };

        var provider = _selector.GetProvider(file, new ConvertSettings { StandardTargetFormat = "BMP" });

        Assert.IsType<SkiaSharpProvider>(provider);
    }
}
