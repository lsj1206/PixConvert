using Moq;
using PixConvert.Models;
using PixConvert.Services;
using Xunit;

namespace PixConvert.Tests;

[Collection("MessengerIsolation")]
public class FileInputSnackbarResultTests
{
    [Fact]
    public async Task DropFilesCommand_WhenAnalyzerThrows_ShouldShowGenericFileAddError()
    {
        string[] paths = [@"C:\Drop\a.png"];
        var pathPicker = new Mock<IPathPickerService>();
        var analyzer = new Mock<IFileAnalyzerService>();
        var snackbar = new Mock<ISnackbarService>();
        analyzer
            .Setup(service => service.ProcessPathsAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<IReadOnlySet<string>?>(),
                It.IsAny<IProgress<FileProcessingProgress>?>()))
            .ThrowsAsync(new InvalidOperationException("raw failure"));
        var vm = FileInputViewModelTestFactory.CreateViewModel(pathPicker, analyzer, snackbar);

        await vm.DropFilesCommand.ExecuteAsync(paths);

        snackbar.Verify(
            service => service.Show("Msg_FileAddError", SnackbarType.Error, It.IsAny<int>()),
            Times.Once);
        snackbar.Verify(
            service => service.Show(It.Is<string>(message => message.Contains("raw failure", StringComparison.OrdinalIgnoreCase)), It.IsAny<SnackbarType>(), It.IsAny<int>()),
            Times.Never);
    }

    [Fact]
    public async Task DropFilesCommand_WhenAllFilesFail_ShouldShowFailedCountMessage()
    {
        string[] paths = [@"C:\Drop\a.png", @"C:\Drop\b.jpg"];
        var pathPicker = new Mock<IPathPickerService>();
        var analyzer = FileInputViewModelTestFactory.CreateAnalyzerReturning(new FileProcessingResult { FailedCount = 2 });
        var snackbar = new Mock<ISnackbarService>();
        var vm = FileInputViewModelTestFactory.CreateViewModel(pathPicker, analyzer, snackbar);

        await vm.DropFilesCommand.ExecuteAsync(paths);

        snackbar.Verify(
            service => service.Show("Msg_AddFileFailed", SnackbarType.Error, It.IsAny<int>()),
            Times.Once);
    }

    [Fact]
    public async Task DropFilesCommand_WhenSomeFilesFail_ShouldShowFailureSummary()
    {
        string[] paths = [@"C:\Drop\a.png", @"C:\Drop\b.jpg"];
        var pathPicker = new Mock<IPathPickerService>();
        var analyzer = FileInputViewModelTestFactory.CreateAnalyzerReturning(new FileProcessingResult
        {
            NewItems = [new FileItem { Path = paths[0] }],
            FailedCount = 1
        });
        var snackbar = new Mock<ISnackbarService>();
        var vm = FileInputViewModelTestFactory.CreateViewModel(pathPicker, analyzer, snackbar);

        await vm.DropFilesCommand.ExecuteAsync(paths);

        snackbar.Verify(
            service => service.Show("Msg_AddWithFailure", SnackbarType.Warning, It.IsAny<int>()),
            Times.Once);
    }

    [Fact]
    public async Task DropFilesCommand_WhenAnalyzerReturnsNoCounts_ShouldShowNoNewFiles()
    {
        string[] paths = [@"C:\EmptyFolder"];
        var pathPicker = new Mock<IPathPickerService>();
        var analyzer = FileInputViewModelTestFactory.CreateAnalyzerReturning(new FileProcessingResult());
        var snackbar = new Mock<ISnackbarService>();
        var vm = FileInputViewModelTestFactory.CreateViewModel(pathPicker, analyzer, snackbar);

        await vm.DropFilesCommand.ExecuteAsync(paths);

        snackbar.Verify(
            service => service.Show("Msg_NoNewFiles", SnackbarType.Error, It.IsAny<int>()),
            Times.Once);
    }

    [Fact]
    public async Task DropFilesCommand_WhenOnlyLimitReached_ShouldShowLimitReached()
    {
        string[] paths = [@"C:\Drop\a.png"];
        var pathPicker = new Mock<IPathPickerService>();
        var analyzer = FileInputViewModelTestFactory.CreateAnalyzerReturning(new FileProcessingResult { IgnoredCount = 1 });
        var snackbar = new Mock<ISnackbarService>();
        var vm = FileInputViewModelTestFactory.CreateViewModel(pathPicker, analyzer, snackbar);

        await vm.DropFilesCommand.ExecuteAsync(paths);

        snackbar.Verify(
            service => service.Show("Msg_LimitReached", SnackbarType.Error, It.IsAny<int>()),
            Times.Once);
    }

    [Fact]
    public async Task DropFilesCommand_WhenOnlyDuplicatesFound_ShouldShowNoNewFiles()
    {
        string[] paths = [@"C:\Drop\a.png"];
        var pathPicker = new Mock<IPathPickerService>();
        var analyzer = FileInputViewModelTestFactory.CreateAnalyzerReturning(new FileProcessingResult { DuplicateCount = 1 });
        var snackbar = new Mock<ISnackbarService>();
        var vm = FileInputViewModelTestFactory.CreateViewModel(pathPicker, analyzer, snackbar);

        await vm.DropFilesCommand.ExecuteAsync(paths);

        snackbar.Verify(
            service => service.Show("Msg_NoNewFiles", SnackbarType.Error, It.IsAny<int>()),
            Times.Once);
    }

    [Fact]
    public async Task DropFilesCommand_WhenSomeFilesAreDuplicates_ShouldShowDuplicateSummary()
    {
        string[] paths = [@"C:\Drop\a.png", @"C:\Drop\b.jpg"];
        var pathPicker = new Mock<IPathPickerService>();
        var analyzer = FileInputViewModelTestFactory.CreateAnalyzerReturning(new FileProcessingResult
        {
            NewItems = [new FileItem { Path = paths[0] }],
            DuplicateCount = 1
        });
        var snackbar = new Mock<ISnackbarService>();
        var vm = FileInputViewModelTestFactory.CreateViewModel(pathPicker, analyzer, snackbar);

        await vm.DropFilesCommand.ExecuteAsync(paths);

        snackbar.Verify(
            service => service.Show("Msg_AddWithDuplicate", SnackbarType.Warning, It.IsAny<int>()),
            Times.Once);
    }
}
