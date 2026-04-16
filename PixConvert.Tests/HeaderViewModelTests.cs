using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PixConvert.Services;
using PixConvert.ViewModels;
using Xunit;

namespace PixConvert.Tests;

public class HeaderViewModelTests
{
    [Fact]
    public async Task ShowAppSettingCommand_ShouldDelegateToDialogService()
    {
        var language = new Mock<ILanguageService>();
        language.Setup(service => service.GetString(It.IsAny<string>())).Returns<string>(key => key);
        var dialog = new Mock<IDialogService>();
        dialog
            .Setup(service => service.ShowAppSettingDialogAsync(It.IsAny<AppSettingViewModel>()))
            .ReturnsAsync(true);
        var fileList = new FileListViewModel(language.Object, NullLogger<FileListViewModel>.Instance);
        var vm = new HeaderViewModel(
            language.Object,
            NullLogger<HeaderViewModel>.Instance,
            fileList,
            dialog.Object,
            () => null!);

        await vm.ShowAppSettingCommand.ExecuteAsync(null);

        dialog.Verify(service => service.ShowAppSettingDialogAsync(It.IsAny<AppSettingViewModel>()), Times.Once);
    }
}
