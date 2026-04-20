using System.IO;
using System.Linq;
using System.Text.Json;
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

    private static SettingService CreateService(string path, string systemLanguage = "ko-KR")
    {
        var language = new Mock<ILanguageService>();
        language.Setup(service => service.GetString(It.IsAny<string>())).Returns<string>(key => key);
        language.Setup(service => service.GetSystemLanguage()).Returns(systemLanguage);

        return new SettingService(NullLogger<SettingService>.Instance, language.Object, path);
    }

    private static AppSettings ReadSettings(string path)
    {
        string json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<AppSettings>(json)
            ?? throw new InvalidOperationException("settings.json was not a valid AppSettings document.");
    }
}
