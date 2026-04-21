using Microsoft.Extensions.Logging;
using Moq;
using PixConvert.Services;

namespace PixConvert.Tests;

internal sealed class FileAnalyzerServiceTestHarness : IDisposable
{
    public Mock<IFileScannerService> Scanner { get; } = new();
    public Mock<ILogger<FileAnalyzerService>> Logger { get; } = new();
    public Mock<ILanguageService> Language { get; } = new();
    public Mock<IDriveInfoService> DriveInfo { get; } = new();
    public TempDirectoryFixture TempDirectory { get; } = new("FileAnalyzerTests_");
    public FileAnalyzerService Service { get; }

    public FileAnalyzerServiceTestHarness()
    {
        Language.Setup(service => service.GetString(It.IsAny<string>()))
            .Returns((string key) => key);
        Language.Setup(service => service.GetString("Log_Process_Summary"))
            .Returns("[FileAnalyzerService] File analysis completed. InputPaths={InputPaths}, Added={Added}, Duplicate={Dup}, Ignored={Ignored}, Failed={Failed}, ElapsedMs={Time}");
        DriveInfo.Setup(service => service.GetOptimalParallelismAsync(It.IsAny<string>()))
            .ReturnsAsync(1);

        Service = new FileAnalyzerService(
            Scanner.Object,
            Logger.Object,
            Language.Object,
            DriveInfo.Object);
    }

    public void Dispose()
    {
        TempDirectory.Dispose();
    }
}
