using Microsoft.Extensions.Logging;
using Moq;
using PixConvert.Services;
using Xunit;

namespace PixConvert.Tests;

public class DialogServiceTests
{
    [Fact]
    public void AppSettingButtonResources_ShouldUseOnlyCloseConfirmButton()
    {
        var buttons = DialogService.AppSettingButtonResources;

        Assert.Null(buttons.PrimaryButtonTextKey);
        Assert.Equal("Dlg_Confirm", buttons.CloseButtonTextKey);
    }

    [Fact]
    public void ConvertSettingButtonResources_ShouldKeepCancelAndConfirmButtons()
    {
        var buttons = DialogService.ConvertSettingButtonResources;

        Assert.Equal("Dlg_Cancel", buttons.PrimaryButtonTextKey);
        Assert.Equal("Dlg_Confirm", buttons.CloseButtonTextKey);
    }

    [Fact]
    public void Constructor_ShouldAcceptLoggerDependency()
    {
        var logger = new Mock<ILogger<DialogService>>();

        var service = new DialogService(logger.Object);

        Assert.NotNull(service);
    }

    [Fact]
    public void HasDialogOwner_WhenMainWindowMissing_ShouldLogWarningAndReturnFalse()
    {
        var logger = new Mock<ILogger<DialogService>>();

        bool result = DialogService.HasDialogOwner(null, logger.Object);

        Assert.False(result);
        VerifyWarningLog(logger, DialogService.LogNoMainWindow);
    }

    [Fact]
    public void TryReserveDialog_WhenAlreadyReserved_ShouldLogWarningAndReturnFalse()
    {
        var logger = new Mock<ILogger<DialogService>>();
        int isDialogOpen = 1;

        bool result = DialogService.TryReserveDialog(ref isDialogOpen, logger.Object);

        Assert.False(result);
        Assert.Equal(1, isDialogOpen);
        VerifyWarningLog(logger, DialogService.LogAlreadyOpen);
    }

    private static void VerifyWarningLog(Mock<ILogger<DialogService>> logger, string expectedMessage)
    {
        logger.Verify(
            service => service.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString() == expectedMessage),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
