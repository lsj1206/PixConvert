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
}
