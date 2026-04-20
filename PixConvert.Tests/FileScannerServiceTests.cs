using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;
using Moq;
using PixConvert.Models;
using PixConvert.Services;
using Xunit;

namespace PixConvert.Tests;

public class FileScannerServiceTests : IDisposable
{
    private readonly FileScannerService _fileScannerService;
    private readonly Mock<ILogger<FileScannerService>> _mockLogger;
    private readonly string _tempDirectory;

    public FileScannerServiceTests()
    {
        _mockLogger = new Mock<ILogger<FileScannerService>>();

        var mockLanguage = new Mock<ILanguageService>();
        mockLanguage.Setup(x => x.GetString(It.IsAny<string>())).Returns((string key) => key);

        _fileScannerService = new FileScannerService(_mockLogger.Object, mockLanguage.Object);
        _tempDirectory = Path.Combine(Path.GetTempPath(), "PixConvertTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public async Task AnalyzeSignatureAsync_GivenRealJpgByteHeader_ShouldReturnJpeg()
    {
        string path = WriteFile("fake_image.png", [0xFF, 0xD8, 0xFF, 0x00]);

        FileSignatureResult result = await _fileScannerService.AnalyzeSignatureAsync(path);

        AssertSignature(result, "JPEG", isAnimation: false, isUnsupported: false);
    }

    [Fact]
    public async Task AnalyzeSignatureAsync_GivenStaticPng_ShouldReturnSupportedPng()
    {
        string path = WriteFile("static.png", Png(("IHDR", new byte[13]), ("IDAT", [])));

        FileSignatureResult result = await _fileScannerService.AnalyzeSignatureAsync(path);

        AssertSignature(result, "PNG", isAnimation: false, isUnsupported: false);
    }

    [Fact]
    public async Task AnalyzeSignatureAsync_GivenApngActlBeforeIdat_ShouldReturnPngAnimationUnsupported()
    {
        byte[] animationControl = [0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00, 0x00];
        string path = WriteFile("animated.png", Png(("IHDR", new byte[13]), ("acTL", animationControl), ("IDAT", [])));

        FileSignatureResult result = await _fileScannerService.AnalyzeSignatureAsync(path);

        AssertSignature(result, "PNG", isAnimation: true, isUnsupported: true);
    }

    [Fact]
    public async Task AnalyzeSignatureAsync_GivenActlAfterIdat_ShouldReturnStaticPng()
    {
        byte[] animationControl = [0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00, 0x00];
        string path = WriteFile("late_actl.png", Png(("IHDR", new byte[13]), ("IDAT", []), ("acTL", animationControl)));

        FileSignatureResult result = await _fileScannerService.AnalyzeSignatureAsync(path);

        AssertSignature(result, "PNG", isAnimation: false, isUnsupported: false);
    }

    [Fact]
    public async Task AnalyzeSignatureAsync_GivenEmptyFile_ShouldReturnUnsupported()
    {
        string path = WriteFile("empty.txt", []);

        FileSignatureResult result = await _fileScannerService.AnalyzeSignatureAsync(path);

        AssertSignature(result, "-", isAnimation: false, isUnsupported: true);
    }

    [Fact]
    public async Task AnalyzeSignatureAsync_GivenNonExistentFile_ShouldReturnUnsupportedAndLogError()
    {
        FileSignatureResult result = await _fileScannerService.AnalyzeSignatureAsync("C:\\NonExistentPath\\Fake.jpg");

        AssertSignature(result, "-", isAnimation: false, isUnsupported: true);
        _mockLogger.Verify(
            x => x.Log(
                It.Is<LogLevel>(l => l == LogLevel.Error),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public async Task AnalyzeSignatureAsync_GivenStaticGif_ShouldReturnGifUnsupported()
    {
        string path = WriteFile("static.gif", Gif(frameCount: 1, version: "89a"));

        FileSignatureResult result = await _fileScannerService.AnalyzeSignatureAsync(path);

        AssertSignature(result, "GIF", isAnimation: false, isUnsupported: true);
    }

    [Fact]
    public async Task AnalyzeSignatureAsync_GivenAnimatedGif_ShouldReturnGifAnimationSupported()
    {
        string path = WriteFile("animated.gif", Gif(frameCount: 2, version: "89a"));

        FileSignatureResult result = await _fileScannerService.AnalyzeSignatureAsync(path);

        AssertSignature(result, "GIF", isAnimation: true, isUnsupported: false);
    }

    [Fact]
    public async Task AnalyzeSignatureAsync_GivenGif87aSingleFrame_ShouldReturnGifUnsupported()
    {
        string path = WriteFile("static87a.gif", Gif(frameCount: 1, version: "87a"));

        FileSignatureResult result = await _fileScannerService.AnalyzeSignatureAsync(path);

        AssertSignature(result, "GIF", isAnimation: false, isUnsupported: true);
    }

    [Fact]
    public async Task AnalyzeSignatureAsync_GivenBmpHeader_ShouldReturnBmpSupported()
    {
        string path = WriteFile("fake.jpg", [0x42, 0x4D, 0x00, 0x00]);

        FileSignatureResult result = await _fileScannerService.AnalyzeSignatureAsync(path);

        AssertSignature(result, "BMP", isAnimation: false, isUnsupported: false);
    }

    [Fact]
    public async Task AnalyzeSignatureAsync_GivenStaticWebpHeader_ShouldReturnWebpSupported()
    {
        string path = WriteFile("static.webp", Webp(isAnimation: false));

        FileSignatureResult result = await _fileScannerService.AnalyzeSignatureAsync(path);

        AssertSignature(result, "WEBP", isAnimation: false, isUnsupported: false);
    }

    [Fact]
    public async Task AnalyzeSignatureAsync_GivenAnimatedWebpHeader_ShouldReturnWebpAnimationSupported()
    {
        string path = WriteFile("animated.webp", Webp(isAnimation: true));

        FileSignatureResult result = await _fileScannerService.AnalyzeSignatureAsync(path);

        AssertSignature(result, "WEBP", isAnimation: true, isUnsupported: false);
    }

    [Fact]
    public async Task AnalyzeSignatureAsync_GivenAvifBrand_ShouldReturnAvifSupported()
    {
        string path = WriteFile("static.avif", Avif("avif", "mif1", "miaf"));

        FileSignatureResult result = await _fileScannerService.AnalyzeSignatureAsync(path);

        AssertSignature(result, "AVIF", isAnimation: false, isUnsupported: false);
    }

    [Fact]
    public async Task AnalyzeSignatureAsync_GivenAvisBrand_ShouldReturnAvifAnimationUnsupported()
    {
        string path = WriteFile("sequence.avif", Avif("avis", "msf1", "miaf"));

        FileSignatureResult result = await _fileScannerService.AnalyzeSignatureAsync(path);

        AssertSignature(result, "AVIF", isAnimation: true, isUnsupported: true);
    }

    [Fact]
    public async Task CreateFileItemAsync_GivenApng_ShouldApplySignatureResult()
    {
        byte[] animationControl = [0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00, 0x00];
        string path = WriteFile("created_apng.png", Png(("IHDR", new byte[13]), ("acTL", animationControl), ("IDAT", [])));

        FileItem? item = await _fileScannerService.CreateFileItemAsync(path);

        Assert.NotNull(item);
        Assert.Equal(path, item.Path);
        Assert.Equal(new FileInfo(path).Length, item.Size);
        Assert.Equal("PNG", item.FileSignature);
        Assert.True(item.IsAnimation);
        Assert.True(item.IsUnsupported);
    }

    [Fact]
    public async Task CreateFileItemAsync_WhenFileIsLocked_ShouldReturnNullAndLogWarning()
    {
        string path = WriteFile("locked.png", [0x89, 0x50]);
        await using var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        FileItem? item = await _fileScannerService.CreateFileItemAsync(path);

        Assert.Null(item);
        VerifyLog(LogLevel.Warning, Times.Once());
        VerifyLog(LogLevel.Error, Times.Never());
    }

    [Fact]
    public async Task CreateFileItemAsync_WhenUnexpectedExceptionOccurs_ShouldReturnNullAndLogError()
    {
        FileItem? item = await _fileScannerService.CreateFileItemAsync("bad\0path.png");

        Assert.Null(item);
        VerifyLog(LogLevel.Error, Times.Once());
    }

    private string WriteFile(string fileName, byte[] bytes)
    {
        string path = Path.Combine(_tempDirectory, fileName);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static void AssertSignature(
        FileSignatureResult result,
        string format,
        bool isAnimation,
        bool isUnsupported)
    {
        Assert.Equal(format, result.Format);
        Assert.Equal(isAnimation, result.IsAnimation);
        Assert.Equal(isUnsupported, result.IsUnsupported);
    }

    private void VerifyLog(LogLevel level, Times times)
    {
        _mockLogger.Verify(
            x => x.Log(
                It.Is<LogLevel>(l => l == level),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception?>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            times);
    }

    private static byte[] Png(params (string Type, byte[] Data)[] chunks)
    {
        using var ms = new MemoryStream();
        ms.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        byte[] lengthBytes = new byte[4];

        foreach (var (type, data) in chunks)
        {
            BinaryPrimitives.WriteUInt32BigEndian(lengthBytes, (uint)data.Length);
            ms.Write(lengthBytes);
            ms.Write(Encoding.ASCII.GetBytes(type));
            ms.Write(data);
            ms.Write([0x00, 0x00, 0x00, 0x00]);
        }

        return ms.ToArray();
    }

    private static byte[] Gif(int frameCount, string version)
    {
        using var ms = new MemoryStream();
        ms.Write(Encoding.ASCII.GetBytes("GIF" + version));
        ms.Write([
            0x01, 0x00, 0x01, 0x00,
            0x80,
            0x00,
            0x00,
            0x00, 0x00, 0x00,
            0xFF, 0xFF, 0xFF
        ]);

        for (int i = 0; i < frameCount; i++)
        {
            ms.Write([
                0x2C,
                0x00, 0x00, 0x00, 0x00,
                0x01, 0x00, 0x01, 0x00,
                0x00,
                0x02,
                0x02, 0x44, 0x01,
                0x00
            ]);
        }

        ms.WriteByte(0x3B);
        return ms.ToArray();
    }

    private static byte[] Webp(bool isAnimation)
    {
        byte[] bytes = new byte[32];
        bytes[0] = 0x52; bytes[1] = 0x49; bytes[2] = 0x46; bytes[3] = 0x46;
        bytes[8] = 0x57; bytes[9] = 0x45; bytes[10] = 0x42; bytes[11] = 0x50;
        bytes[12] = 0x56; bytes[13] = 0x50; bytes[14] = 0x38; bytes[15] = 0x58;
        bytes[20] = isAnimation ? (byte)0x02 : (byte)0x00;
        return bytes;
    }

    private static byte[] Avif(string majorBrand, params string[] compatibleBrands)
    {
        using var ms = new MemoryStream();
        uint size = (uint)(16 + compatibleBrands.Length * 4);
        Span<byte> sizeBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(sizeBytes, size);
        ms.Write(sizeBytes);
        ms.Write(Encoding.ASCII.GetBytes("ftyp"));
        ms.Write(Encoding.ASCII.GetBytes(majorBrand));
        ms.Write([0x00, 0x00, 0x00, 0x00]);

        foreach (string brand in compatibleBrands)
            ms.Write(Encoding.ASCII.GetBytes(brand));

        return ms.ToArray();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, true);
    }
}
