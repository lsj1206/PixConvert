using Microsoft.Extensions.Logging.Abstractions;
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
        vm.OpenDataFolderCommand.Execute(null);

        launcher.Verify(service => service.OpenUrl("https://github.com/lsj1206/PixConvert"), Times.Once);
        launcher.Verify(service => service.OpenFolder(@"C:\Apps\PixConvert"), Times.Once);
    }

    [Fact]
    public void ConfirmDeletion_WhenChanged_ShouldUpdateRuntimeStateAndRequestSave()
    {
        var settings = new AppSettings { ConfirmDeletion = true, Language = "ko-KR" };
        var settingService = CreateSettingService(settings);
        var language = CreateLanguageService();
        var listManager = CreateListManager(language.Object);
        var vm = CreateViewModel(language: language, settingService: settingService, listManager: listManager);

        vm.ConfirmDeletion = false;

        Assert.False(settings.ConfirmDeletion);
        Assert.False(listManager.ConfirmDeletion);
        settingService.Verify(service => service.SaveAsync(), Times.Once);
    }

    [Fact]
    public void CurrentLanguageCode_WhenChanged_ShouldChangeLanguageUpdateSettingsAndRequestSave()
    {
        var settings = new AppSettings { ConfirmDeletion = true, Language = "ko-KR" };
        var settingService = CreateSettingService(settings);
        var language = CreateLanguageService(currentLanguage: "ko-KR");
        var vm = CreateViewModel(language: language, settingService: settingService);

        vm.CurrentLanguageCode = "en-US";

        language.Verify(service => service.ChangeLanguage("en-US"), Times.Once);
        Assert.Equal("en-US", settings.Language);
        settingService.Verify(service => service.SaveAsync(), Times.Once);
    }

    [Fact]
    public void SettingChange_WhenSaveFails_ShouldNotCreateUiErrorState()
    {
        var settings = new AppSettings { ConfirmDeletion = true, Language = "ko-KR" };
        var settingService = CreateSettingService(settings, saveResult: false);
        var vm = CreateViewModel(settingService: settingService);

        vm.ConfirmDeletion = false;

        settingService.Verify(service => service.SaveAsync(), Times.Once);
        Assert.Equal(string.Empty, vm.UpdateStatusText);
    }

    private static AppSettingViewModel CreateViewModel(
        Mock<ILanguageService>? language = null,
        Mock<IAppInfoService>? appInfo = null,
        Mock<IExternalLauncher>? launcher = null,
        Mock<ISettingService>? settingService = null,
        AppSettings? settings = null,
        ListManagerViewModel? listManager = null)
    {
        language ??= CreateLanguageService();

        settingService ??= CreateSettingService(settings ?? new AppSettings());

        listManager ??= CreateListManager(language.Object);

        return new AppSettingViewModel(
            language.Object,
            NullLogger<AppSettingViewModel>.Instance,
            settingService.Object,
            (appInfo ?? CreateAppInfoService()).Object,
            (launcher ?? new Mock<IExternalLauncher>()).Object,
            listManager);
    }

    private static Mock<ISettingService> CreateSettingService(AppSettings settings, bool saveResult = true)
    {
        var settingService = new Mock<ISettingService>();
        settingService.Setup(service => service.Settings).Returns(settings);
        settingService.Setup(service => service.SaveAsync()).ReturnsAsync(saveResult);
        return settingService;
    }

    private static Mock<ILanguageService> CreateLanguageService(string currentLanguage = "ko-KR")
    {
        var language = new Mock<ILanguageService>();
        language.Setup(service => service.GetCurrentLanguage()).Returns(currentLanguage);
        language.Setup(service => service.GetString(It.IsAny<string>())).Returns<string>(key => key);
        language.Setup(service => service.GetString("Setting_App_UpdateChecking")).Returns("Checking...");
        language.Setup(service => service.GetString("Setting_App_UpdateAvailable")).Returns("New version {0} is available.");
        return language;
    }

    private static ListManagerViewModel CreateListManager(ILanguageService language)
    {
        var fileList = new FileListViewModel(language, NullLogger<FileListViewModel>.Instance);
        return new ListManagerViewModel(
            NullLogger<ListManagerViewModel>.Instance,
            language,
            new Mock<ISnackbarService>().Object,
            new Mock<IDialogService>().Object,
            fileList);
    }

    private static Mock<IAppInfoService> CreateAppInfoService()
    {
        var appInfo = new Mock<IAppInfoService>();
        appInfo.SetupGet(service => service.RepositoryUrl).Returns("https://github.com/lsj1206/PixConvert");
        appInfo.SetupGet(service => service.DataFolderPath).Returns(@"C:\Apps\PixConvert");
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
