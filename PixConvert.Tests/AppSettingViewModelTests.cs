using Microsoft.Extensions.Logging.Abstractions;
using ModernWpf;
using Moq;
using PixConvert.Models;
using PixConvert.Services;
using PixConvert.Services.Interfaces;
using PixConvert.ViewModels;
using Xunit;

namespace PixConvert.Tests;

public class AppSettingViewModelTests
{
    [Fact]
    public async Task CheckUpdateCommand_ShouldExposeCheckingStateAndResultText()
    {
        var tcs = new TaskCompletionSource<UpdateCheckResult>();
        var appInfo = CreateAppInfoService();
        appInfo
            .Setup(service => service.CheckLatestReleaseAsync(It.IsAny<CancellationToken>()))
            .Returns(tcs.Task);

        var vm = CreateViewModel(appInfo: appInfo);

        var executeTask = vm.CheckUpdateCommand.ExecuteAsync(null);

        Assert.True(vm.IsCheckingUpdate);
        Assert.Equal("Checking...", vm.UpdateStatusText);

        tcs.SetResult(new UpdateCheckResult(
            UpdateCheckStatus.UpdateAvailable,
            "v1.0.0",
            "v2.0.0",
            "https://example.com/release",
            "Setting_App_UpdateAvailable"));
        await executeTask;

        Assert.False(vm.IsCheckingUpdate);
        Assert.Equal("New version v2.0.0 is available.", vm.UpdateStatusText);
    }

    [Fact]
    public void OpenCommands_ShouldDelegateToExternalLauncher()
    {
        var launcher = new Mock<IExternalLauncher>();
        var appInfo = CreateAppInfoService();
        var vm = CreateViewModel(appInfo: appInfo, launcher: launcher);

        vm.OpenGitHubCommand.Execute(null);
        vm.OpenAppDataFolderCommand.Execute(null);

        launcher.Verify(service => service.OpenUrl("https://github.com/lsj1206/PixConvert"), Times.Once);
        launcher.Verify(service => service.OpenFolder(@"C:\Users\Test\AppData\Roaming\PixConvert"), Times.Once);
    }

    [Fact]
    public void Constructor_ShouldExposeSavedTheme()
    {
        var vm = CreateViewModel(settings: new AppSettings { Theme = "Dark" });

        Assert.Equal("Dark", vm.CurrentTheme);
    }

    [Theory]
    [InlineData("System")]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void CurrentTheme_WhenChanged_ShouldSaveAndApplyTheme(string selectedTheme)
    {
        var previousTheme = ThemeManager.Current.ApplicationTheme;
        var initialTheme = selectedTheme == "Light" ? "Dark" : "Light";
        var settings = new AppSettings { Theme = initialTheme };
        var settingService = CreateSettingService(settings);
        var vm = CreateViewModel(settingService: settingService);

        try
        {
            vm.CurrentTheme = selectedTheme;

            Assert.Equal(selectedTheme, settings.Theme);
            Assert.Equal(ToApplicationTheme(selectedTheme), ThemeManager.Current.ApplicationTheme);
            settingService.Verify(service => service.SaveAsync(), Times.Once);
        }
        finally
        {
            ThemeManager.Current.ApplicationTheme = previousTheme;
        }
    }

    private static AppSettingViewModel CreateViewModel(
        Mock<IAppInfoService>? appInfo = null,
        Mock<IExternalLauncher>? launcher = null,
        Mock<ISettingService>? settingService = null,
        AppSettings? settings = null)
    {
        var language = new Mock<ILanguageService>();
        language.Setup(service => service.GetCurrentLanguage()).Returns("ko-KR");
        language.Setup(service => service.GetString(It.IsAny<string>())).Returns<string>(key => key);
        language.Setup(service => service.GetString("Setting_App_UpdateChecking")).Returns("Checking...");
        language.Setup(service => service.GetString("Setting_App_UpdateAvailable")).Returns("New version {0} is available.");

        settingService ??= CreateSettingService(settings ?? new AppSettings());

        var fileList = new FileListViewModel(language.Object, NullLogger<FileListViewModel>.Instance);
        var listManager = new ListManagerViewModel(
            NullLogger<ListManagerViewModel>.Instance,
            language.Object,
            new Mock<ISnackbarService>().Object,
            new Mock<IDialogService>().Object,
            fileList);

        return new AppSettingViewModel(
            language.Object,
            NullLogger<AppSettingViewModel>.Instance,
            settingService.Object,
            (appInfo ?? CreateAppInfoService()).Object,
            (launcher ?? new Mock<IExternalLauncher>()).Object,
            listManager);
    }

    private static Mock<ISettingService> CreateSettingService(AppSettings settings)
    {
        var settingService = new Mock<ISettingService>();
        settingService.Setup(service => service.Settings).Returns(settings);
        settingService.Setup(service => service.SaveAsync()).ReturnsAsync(true);
        return settingService;
    }

    private static ApplicationTheme? ToApplicationTheme(string theme)
    {
        return theme switch
        {
            "Light" => ApplicationTheme.Light,
            "Dark" => ApplicationTheme.Dark,
            _ => null
        };
    }

    private static Mock<IAppInfoService> CreateAppInfoService()
    {
        var appInfo = new Mock<IAppInfoService>();
        appInfo.SetupGet(service => service.RepositoryUrl).Returns("https://github.com/lsj1206/PixConvert");
        appInfo.SetupGet(service => service.AppDataFolderPath).Returns(@"C:\Users\Test\AppData\Roaming\PixConvert");
        appInfo
            .Setup(service => service.GetEngineInfo())
            .Returns(new[]
            {
                new AppEngineInfo("SkiaSharp", "3.119.2"),
                new AppEngineInfo("NetVips", "3.2.0")
            });
        return appInfo;
    }
}
