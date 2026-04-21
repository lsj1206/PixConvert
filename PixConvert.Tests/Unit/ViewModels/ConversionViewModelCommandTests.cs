using Moq;
using PixConvert.Models;
using PixConvert.Services;
using PixConvert.ViewModels;
using Xunit;

namespace PixConvert.Tests;

public class ConversionViewModelCommandTests
{
    [Fact]
    public void Commands_WhenConverting_ShouldDisableStartAndEnableCancel()
    {
        var context = ConversionViewModelTestHarness.CreateDefaultContext();
        context.ViewModel.CurrentStatus = AppStatus.Converting;

        Assert.False(context.ViewModel.OpenConvertSettingCommand.CanExecute(null));
        Assert.False(context.ViewModel.ConvertFilesCommand.CanExecute(null));
        Assert.True(context.ViewModel.CancelConvertCommand.CanExecute(null));
    }

    [Fact]
    public async Task OpenConvertSettingCommand_WhenConfirmed_ShouldSavePreset()
    {
        var context = ConversionViewModelTestHarness.CreateDefaultContext();
        context.DialogService
            .Setup(service => service.ShowConvertSettingDialogAsync(It.IsAny<ConvertSettingViewModel>()))
            .ReturnsAsync(true);

        await context.ViewModel.OpenConvertSettingCommand.ExecuteAsync(null);

        context.DialogService.Verify(
            service => service.ShowConvertSettingDialogAsync(It.IsAny<ConvertSettingViewModel>()),
            Times.Once);
        context.PresetService.Verify(service => service.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task ConvertFilesCommand_WhenNoInputFiles_ShouldShowWarningWithoutConfirmation()
    {
        var context = ConversionViewModelTestHarness.CreateDefaultContext();

        await context.ViewModel.ConvertFilesCommand.ExecuteAsync(null);

        context.DialogService.Verify(
            service => service.ShowConfirmationAsync(It.IsAny<string>(), "Dlg_Title_Convert", It.IsAny<string?>()),
            Times.Never);
        context.SnackbarService.Verify(
            service => service.Show("Msg_Error_NoTargetFiles", SnackbarType.Warning, It.IsAny<int>()),
            Times.Once);
    }

    [Fact]
    public async Task ConvertFilesCommand_WhenActivePresetIsMissing_ShouldShowPresetError()
    {
        var context = ConversionViewModelTestHarness.CreateDefaultContext();
        context.FileList.AddItem(new FileItem { Path = @"C:\input\test.png", FileSignature = "PNG" });

        await context.ViewModel.ConvertFilesCommand.ExecuteAsync(null);

        context.DialogService.Verify(
            service => service.ShowConfirmationAsync(It.IsAny<string>(), "Dlg_Title_Convert", It.IsAny<string?>()),
            Times.Never);
        context.SnackbarService.Verify(
            service => service.Show("Msg_Error_EmptyPreset", SnackbarType.Error, It.IsAny<int>()),
            Times.Once);
    }

    [Fact]
    public async Task ConvertFilesCommand_WhenPresetValidationFails_ShouldShowValidationMessage()
    {
        var context = ConversionViewModelTestHarness.CreateDefaultContext();
        var preset = new ConvertPreset
        {
            Name = "Default",
            Settings = new ConvertSettings()
        };
        string errorKey = "Msg_InvalidPreset";

        context.FileList.AddItem(new FileItem { Path = @"C:\input\test.png", FileSignature = "PNG", IsUnsupported = false });
        context.PresetService.Setup(service => service.ActivePreset).Returns(preset);
        context.PresetService
            .Setup(service => service.ValidPresetData(It.IsAny<ConvertSettings>(), out errorKey))
            .Returns(false);
        context.DialogService
            .Setup(service => service.ShowConfirmationAsync(It.IsAny<string>(), "Dlg_Title_Convert", It.IsAny<string?>()))
            .ReturnsAsync(true);

        await context.ViewModel.ConvertFilesCommand.ExecuteAsync(null);

        context.SnackbarService.Verify(
            service => service.Show("Msg_InvalidPreset", SnackbarType.Error, It.IsAny<int>()),
            Times.Once);
    }

    [Fact]
    public async Task ConfirmCompletionCommand_WhenConversionCompleted_ShouldResetStateAndDisableCommands()
    {
        await ConversionViewModelTestHarness.RunOnStaDispatcherAsync(async () =>
        {
            var files = ConversionViewModelTestHarness.CreateFiles(1);
            var provider = new ConversionViewModelTestHarness.ScriptedProvider(
                (file, token) => Task.FromResult(new ConversionResult(FileConvertStatus.Success, file.Path + ".out", 10)));
            var vm = ConversionViewModelTestHarness.CreateExecutionViewModel(files, provider);

            await vm.ConvertFilesCommand.ExecuteAsync(null);

            Assert.True(vm.IsConversionCompleted);
            Assert.True(vm.ConfirmCompletionCommand.CanExecute(null));
            Assert.False(vm.CancelConvertCommand.CanExecute(null));

            vm.ConfirmCompletionCommand.Execute(null);

            Assert.False(vm.IsConversionCompleted);
            Assert.Equal(AppStatus.Idle, vm.CurrentStatus);
            Assert.Equal(0, vm.ConvertProgressPercent);
            Assert.Equal(0, vm.ProcessedCount);
            Assert.Equal(0, vm.TotalConvertCount);
            Assert.Equal(0, vm.FailCount);
            Assert.Equal(string.Empty, vm.CurrentFileName);
            Assert.False(vm.ConfirmCompletionCommand.CanExecute(null));
            Assert.False(vm.CancelConvertCommand.CanExecute(null));
        });
    }
}
