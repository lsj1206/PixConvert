using Microsoft.Extensions.Logging;
using PixConvert.Models;
using PixConvert.Services;
using PixConvert.Services.Interfaces;
using PixConvert.Services.Providers;
using PixConvert.ViewModels;
using Moq;
using Xunit;

namespace PixConvert.Tests;

public class ConversionViewModelTests
{
    private readonly Mock<IPresetService> _mockPreset;
    private readonly FileListViewModel _fileListVm;
    private readonly ConversionViewModel _vm;

    public ConversionViewModelTests()
    {
        var mockLang = new Mock<ILanguageService>();
        var mockLogger = new Mock<ILogger<ConversionViewModel>>();
        _mockPreset = new Mock<IPresetService>();
        var mockSnackbar = new Mock<ISnackbarService>();
        var mockDialog = new Mock<IDialogService>();

        mockLang.Setup(l => l.GetString(It.IsAny<string>())).Returns<string>(s => s);
        _mockPreset.Setup(p => p.Config).Returns(new PresetConfig());
        _mockPreset.Setup(p => p.ActivePreset).Returns((ConvertPreset?)null);

        var mockSkia = new Mock<SkiaSharpProvider>(mockLang.Object, new Mock<ILogger<SkiaSharpProvider>>().Object);
        var mockVips = new Mock<NetVipsProvider>(mockLang.Object, new Mock<ILogger<NetVipsProvider>>().Object);
        mockSkia.As<IProviderService>().Setup(p => p.Name).Returns("SkiaSharp");
        mockVips.As<IProviderService>().Setup(p => p.Name).Returns("NetVips");
        var engineSelector = new EngineSelector(mockSkia.Object, mockVips.Object);

        _fileListVm = new FileListViewModel(mockLang.Object, new Mock<ILogger<FileListViewModel>>().Object);

        _vm = new ConversionViewModel(
            mockLogger.Object,
            mockLang.Object,
            mockDialog.Object,
            mockSnackbar.Object,
            _mockPreset.Object,
            _fileListVm,
            engineSelector,
            () => null!);
    }

    [Fact]
    public void DefaultState_ShouldInitializeAsEmptyPreset()
    {
        Assert.Equal("Converting_SelectPreset", _vm.ActivePresetName);
        Assert.False(_vm.IsActivePresetValid);
    }

    [Fact]
    public void Items_ShouldExposeUnderlyingFileListItems()
    {
        _fileListVm.AddItem(new FileItem { Path = @"C:\test.png" });

        Assert.Single(_vm.Items);
        Assert.Equal(@"C:\test.png", _vm.Items[0].Path);
    }

    [Fact]
    public void HasFailures_ShouldTrackFailCount()
    {
        Assert.False(_vm.HasFailures);

        _vm.FailCount = 1;

        Assert.True(_vm.HasFailures);
    }

    [Fact]
    public void Commands_WhenConverting_ShouldDisableStartAndEnableCancel()
    {
        _vm.CurrentStatus = AppStatus.Converting;

        Assert.False(_vm.OpenConvertSettingCommand.CanExecute(null));
        Assert.False(_vm.ConvertFilesCommand.CanExecute(null));
        Assert.True(_vm.CancelConvertCommand.CanExecute(null));
    }
}
