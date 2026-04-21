using Moq;
using PixConvert.Models;
using PixConvert.Services;
using Xunit;

namespace PixConvert.Tests;

[Collection("MessengerIsolation")]
public class FileInputStatusFlowTests
{
    [Fact]
    public async Task DropFilesCommand_WhenStartedFromListManager_ShouldRequestListManagerAfterCompletion()
    {
        string[] paths = [@"C:\Drop\a.png"];
        var pathPicker = new Mock<IPathPickerService>();
        var analyzer = FileInputViewModelTestFactory.CreateAnalyzerReturning(paths[0]);
        var vm = FileInputViewModelTestFactory.CreateViewModel(pathPicker, analyzer);
        using var statusRequests = new StatusRequestRecorder();

        vm.CurrentStatus = AppStatus.ListManager;

        await vm.DropFilesCommand.ExecuteAsync(paths);

        Assert.Equal(
            [AppStatus.FileAdd, AppStatus.ListManager],
            statusRequests.Requests);
    }

    [Fact]
    public async Task DropFilesCommand_WhenStartedFromIdle_ShouldRequestIdleAfterCompletion()
    {
        string[] paths = [@"C:\Drop\a.png"];
        var pathPicker = new Mock<IPathPickerService>();
        var analyzer = FileInputViewModelTestFactory.CreateAnalyzerReturning(paths[0]);
        var vm = FileInputViewModelTestFactory.CreateViewModel(pathPicker, analyzer);
        using var statusRequests = new StatusRequestRecorder();

        await vm.DropFilesCommand.ExecuteAsync(paths);

        Assert.Equal(
            [AppStatus.FileAdd, AppStatus.Idle],
            statusRequests.Requests);
    }
}
