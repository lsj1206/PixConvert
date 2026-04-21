using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PixConvert.Models;
using PixConvert.Services;
using Xunit;

namespace PixConvert.Tests;

public class SettingServiceTests : IDisposable
{
    private readonly string _tempDirectory;

    public SettingServiceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "PixConvertSettings_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public async Task LoadAsync_WhenFileMissing_ShouldSaveDefaultsBeforeReturning()
    {
        string path = SettingsPath();
        var service = CreateService(path, systemLanguage: "ko-KR");

        await service.LoadAsync();

        Assert.True(File.Exists(path));
        Assert.False(File.Exists(path + ".bak"));
        var settings = ReadSettings(path);
        Assert.Equal("ko-KR", settings.Language);
        Assert.True(settings.ConfirmDeletion);
    }

    [Fact]
    public async Task LoadAsync_WhenFileEmpty_ShouldSaveDefaultsBeforeReturning()
    {
        string path = SettingsPath();
        await File.WriteAllTextAsync(path, string.Empty);
        var service = CreateService(path, systemLanguage: "en-US");

        await service.LoadAsync();

        var settings = ReadSettings(path);
        Assert.Equal("en-US", settings.Language);
        Assert.True(settings.ConfirmDeletion);
        Assert.False(File.Exists(path + ".bak"));
    }

    [Fact]
    public async Task LoadAsync_WhenLanguageUnsupported_ShouldPersistSystemLanguage()
    {
        string path = SettingsPath();
        await File.WriteAllTextAsync(path, """{"Language":"fr-FR","ConfirmDeletion":false}""");
        var service = CreateService(path, systemLanguage: "en-US");

        await service.LoadAsync();

        Assert.Equal("en-US", service.Settings.Language);
        Assert.False(service.Settings.ConfirmDeletion);

        var settings = ReadSettings(path);
        Assert.Equal("en-US", settings.Language);
        Assert.False(settings.ConfirmDeletion);
        Assert.False(File.Exists(path + ".bak"));
    }

    [Fact]
    public async Task LoadAsync_WhenJsonInvalid_ShouldSaveDefaultsBeforeReturning()
    {
        string path = SettingsPath();
        await File.WriteAllTextAsync(path, "{ invalid json");
        var service = CreateService(path, systemLanguage: "ko-KR");

        await service.LoadAsync();

        var settings = ReadSettings(path);
        Assert.Equal("ko-KR", settings.Language);
        Assert.True(settings.ConfirmDeletion);
        Assert.True(File.Exists(path + ".bak"));
        Assert.Equal("{ invalid json", File.ReadAllText(path + ".bak"));
    }

    [Fact]
    public async Task LoadAsync_WhenBackupFails_ShouldStillSaveDefaults()
    {
        string path = SettingsPath();
        await File.WriteAllTextAsync(path, "{ invalid json");
        Directory.CreateDirectory(path + ".bak");
        var logger = new Mock<ILogger<SettingService>>();
        var service = CreateService(path, systemLanguage: "en-US", logger: logger);

        await service.LoadAsync();

        var settings = ReadSettings(path);
        Assert.Equal("en-US", settings.Language);
        Assert.True(settings.ConfirmDeletion);
        VerifyLog(logger, LogLevel.Warning, "Log_Setting_FileBackupFailed", Times.Once());
    }

    [Fact]
    public async Task SaveAsync_WhenCalledInParallel_ShouldLeaveValidSettingsJson()
    {
        string path = SettingsPath();
        var service = CreateService(path);
        service.Settings.Language = "ko-KR";
        service.Settings.ConfirmDeletion = false;

        bool[] results = await Task.WhenAll(Enumerable.Range(0, 100).Select(_ => service.SaveAsync()));

        Assert.All(results, Assert.True);

        var settings = ReadSettings(path);
        Assert.Equal("ko-KR", settings.Language);
        Assert.False(settings.ConfirmDeletion);
    }

    [Fact]
    public async Task SaveAsync_WhenDirectoryIsMissing_ShouldCreateDirectoryAndSave()
    {
        string path = Path.Combine(_tempDirectory, "nested", "settings.json");
        var service = CreateService(path);
        service.Settings.Language = "en-US";

        bool result = await service.SaveAsync();

        Assert.True(result);
        Assert.True(File.Exists(path));
        Assert.Equal("en-US", ReadSettings(path).Language);
    }

    [Fact]
    public async Task SaveAsync_WhenWriteFails_ShouldReturnFalseWithoutThrowing()
    {
        string path = SettingsPath();
        Directory.CreateDirectory(path);
        var service = CreateService(path);

        bool result = await service.SaveAsync();

        Assert.False(result);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private string SettingsPath() => Path.Combine(_tempDirectory, "settings.json");

    private static SettingService CreateService(
        string path,
        string systemLanguage = "ko-KR",
        Mock<ILogger<SettingService>>? logger = null)
    {
        var language = new Mock<ILanguageService>();
        language.Setup(service => service.GetString(It.IsAny<string>())).Returns<string>(key => key);
        language.Setup(service => service.GetSystemLanguage()).Returns(systemLanguage);

        return new SettingService(logger?.Object ?? NullLogger<SettingService>.Instance, language.Object, path);
    }

    private static AppSettings ReadSettings(string path)
    {
        string json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<AppSettings>(json)
            ?? throw new InvalidOperationException("settings.json was not a valid AppSettings document.");
    }

    private static void VerifyLog(
        Mock<ILogger<SettingService>> logger,
        LogLevel level,
        string expectedMessage,
        Times times)
    {
        logger.Verify(
            service => service.Log(
                It.Is<LogLevel>(actual => actual == level),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString() == expectedMessage),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times);
    }
}
