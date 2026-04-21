using Moq;
using PixConvert.Models;
using PixConvert.Services;
using System.IO;
using Xunit;

namespace PixConvert.Tests;

[Collection("MessengerIsolation")]
public class FileInputCommandRoutingTests
{
    public static IEnumerable<object[]> SuccessfulCommandCases()
    {
        yield return new object[] { "AddFiles", new[] { @"C:\Input\a.png", @"C:\Input\b.jpg" } };
        yield return new object[] { "AddFolder", new[] { @"C:\Input", @"D:\MoreInput" } };
        yield return new object[] { "DropFiles", new[] { @"C:\Drop\a.png", @"C:\Drop\b.jpg" } };
    }

    [Fact]
    public async Task AddFilesCommand_WhenPickerCancelled_ShouldNotAnalyzePaths()
    {
        var pathPicker = new Mock<IPathPickerService>();
        pathPicker
            .Setup(service => service.PickFiles("Dlg_Title_AddFile"))
            .Returns(Array.Empty<string>());
        var analyzer = new Mock<IFileAnalyzerService>();
        var vm = FileInputViewModelTestFactory.CreateViewModel(pathPicker, analyzer);

        await vm.AddFilesCommand.ExecuteAsync(null);

        analyzer.Verify(
            service => service.ProcessPathsAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<IReadOnlySet<string>?>(),
                It.IsAny<IProgress<FileProcessingProgress>?>()),
            Times.Never);
    }

    [Theory]
    [MemberData(nameof(SuccessfulCommandCases))]
    public async Task InputCommand_WhenPathsAreProvided_ShouldAnalyzeSelectedPaths(
        string commandName,
        string[] paths)
    {
        var pathPicker = new Mock<IPathPickerService>();
        if (commandName == "AddFiles")
        {
            pathPicker
                .Setup(service => service.PickFiles("Dlg_Title_AddFile"))
                .Returns(paths);
        }
        else if (commandName == "AddFolder")
        {
            pathPicker
                .Setup(service => service.PickFolders("Dlg_Title_AddFolder"))
                .Returns(paths);
        }

        string analyzedPath = commandName == "AddFolder"
            ? Path.Combine(paths[0], "a.png")
            : paths[0];
        var analyzer = FileInputViewModelTestFactory.CreateAnalyzerReturning(analyzedPath);
        var vm = FileInputViewModelTestFactory.CreateViewModel(pathPicker, analyzer);

        switch (commandName)
        {
            case "AddFiles":
                await vm.AddFilesCommand.ExecuteAsync(null);
                break;
            case "AddFolder":
                await vm.AddFolderCommand.ExecuteAsync(null);
                break;
            default:
                await vm.DropFilesCommand.ExecuteAsync(paths);
                break;
        }

        analyzer.Verify(
            service => service.ProcessPathsAsync(
                It.Is<IEnumerable<string>>(value => value.SequenceEqual(paths)),
                10000,
                0,
                It.IsAny<IReadOnlySet<string>?>(),
                It.IsAny<IProgress<FileProcessingProgress>?>()),
            Times.Once);
    }

    [Fact]
    public async Task DropFilesCommand_WhenConverting_ShouldNotAnalyzeDroppedPaths()
    {
        string[] paths = [@"C:\Drop\a.png"];
        var pathPicker = new Mock<IPathPickerService>();
        var analyzer = FileInputViewModelTestFactory.CreateAnalyzerReturning(paths[0]);
        var vm = FileInputViewModelTestFactory.CreateViewModel(pathPicker, analyzer);

        vm.CurrentStatus = AppStatus.Converting;

        Assert.False(vm.DropFilesCommand.CanExecute(paths));
        if (vm.DropFilesCommand.CanExecute(paths))
        {
            await vm.DropFilesCommand.ExecuteAsync(paths);
        }

        analyzer.Verify(
            service => service.ProcessPathsAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<IReadOnlySet<string>?>(),
                It.IsAny<IProgress<FileProcessingProgress>?>()),
            Times.Never);
    }
}
