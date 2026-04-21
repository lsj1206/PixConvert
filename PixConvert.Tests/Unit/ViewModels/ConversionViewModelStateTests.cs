using PixConvert.Models;
using Xunit;

namespace PixConvert.Tests;

public class ConversionViewModelStateTests
{
    [Fact]
    public void DefaultState_ShouldInitializeAsEmptyPreset()
    {
        var context = ConversionViewModelTestHarness.CreateDefaultContext();

        Assert.Equal("Converting_SelectPreset", context.ViewModel.ActivePresetName);
        Assert.False(context.ViewModel.IsActivePresetValid);
    }

    [Fact]
    public void Items_ShouldExposeUnderlyingFileListItems()
    {
        var context = ConversionViewModelTestHarness.CreateDefaultContext();
        context.FileList.AddItem(new FileItem { Path = @"C:\test.png" });

        Assert.Single(context.ViewModel.Items);
        Assert.Equal(@"C:\test.png", context.ViewModel.Items[0].Path);
    }

    [Fact]
    public void HasFailures_ShouldTrackFailCount()
    {
        var context = ConversionViewModelTestHarness.CreateDefaultContext();

        Assert.False(context.ViewModel.HasFailures);

        context.ViewModel.FailCount = 1;

        Assert.True(context.ViewModel.HasFailures);
    }
}
