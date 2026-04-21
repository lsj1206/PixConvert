using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PixConvert.Models;
using PixConvert.Services;
using PixConvert.ViewModels;
using Xunit;

namespace PixConvert.Tests;

[Collection("MessengerTests")]
public class FileInputViewModelTests
{
    [Fact]
    public async Task AddFilesCommand_WhenPickerCancelled_ShouldNotAnalyzePaths()
    {
        var pathPicker = new Mock<IPathPickerService>();
        pathPicker
            .Setup(service => service.PickFiles("Dlg_Title_AddFile"))
            .Returns(Array.Empty<string>());
        var analyzer = new Mock<IFileAnalyzerService>();
        var vm = CreateViewModel(pathPicker, analyzer);

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

    [Fact]
    public async Task AddFilesCommand_WhenFilesSelected_ShouldAnalyzeSelectedPaths()
    {
        string[] paths = [@"C:\Input\a.png", @"C:\Input\b.jpg"];
        var pathPicker = new Mock<IPathPickerService>();
        pathPicker
            .Setup(service => service.PickFiles("Dlg_Title_AddFile"))
            .Returns(paths);
        var analyzer = CreateAnalyzerReturning(paths[0]);
        var vm = CreateViewModel(pathPicker, analyzer);

        await vm.AddFilesCommand.ExecuteAsync(null);

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
    public async Task AddFolderCommand_WhenFoldersSelected_ShouldAnalyzeSelectedFolders()
    {
        string[] paths = [@"C:\Input", @"D:\MoreInput"];
        var pathPicker = new Mock<IPathPickerService>();
        pathPicker
            .Setup(service => service.PickFolders("Dlg_Title_AddFolder"))
            .Returns(paths);
        var analyzer = CreateAnalyzerReturning(@"C:\Input\a.png");
        var vm = CreateViewModel(pathPicker, analyzer);

        await vm.AddFolderCommand.ExecuteAsync(null);

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
    public async Task DropFilesCommand_WhenFilesDropped_ShouldAnalyzeDroppedPaths()
    {
        string[] paths = [@"C:\Drop\a.png", @"C:\Drop\b.jpg"];
        var pathPicker = new Mock<IPathPickerService>();
        var analyzer = CreateAnalyzerReturning(paths[0]);
        var vm = CreateViewModel(pathPicker, analyzer);

        await vm.DropFilesCommand.ExecuteAsync(paths);

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
        var vm = CreateViewModel(pathPicker, analyzer, snackbar);

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
        var analyzer = CreateAnalyzerReturning(new FileProcessingResult { FailedCount = 2 });
        var snackbar = new Mock<ISnackbarService>();
        var vm = CreateViewModel(pathPicker, analyzer, snackbar);

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
        var analyzer = CreateAnalyzerReturning(new FileProcessingResult
        {
            NewItems = [new FileItem { Path = paths[0] }],
            FailedCount = 1
        });
        var snackbar = new Mock<ISnackbarService>();
        var vm = CreateViewModel(pathPicker, analyzer, snackbar);

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
        var analyzer = CreateAnalyzerReturning(new FileProcessingResult());
        var snackbar = new Mock<ISnackbarService>();
        var vm = CreateViewModel(pathPicker, analyzer, snackbar);

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
        var analyzer = CreateAnalyzerReturning(new FileProcessingResult { IgnoredCount = 1 });
        var snackbar = new Mock<ISnackbarService>();
        var vm = CreateViewModel(pathPicker, analyzer, snackbar);

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
        var analyzer = CreateAnalyzerReturning(new FileProcessingResult { DuplicateCount = 1 });
        var snackbar = new Mock<ISnackbarService>();
        var vm = CreateViewModel(pathPicker, analyzer, snackbar);

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
        var analyzer = CreateAnalyzerReturning(new FileProcessingResult
        {
            NewItems = [new FileItem { Path = paths[0] }],
            DuplicateCount = 1
        });
        var snackbar = new Mock<ISnackbarService>();
        var vm = CreateViewModel(pathPicker, analyzer, snackbar);

        await vm.DropFilesCommand.ExecuteAsync(paths);

        snackbar.Verify(
            service => service.Show("Msg_AddWithDuplicate", SnackbarType.Warning, It.IsAny<int>()),
            Times.Once);
    }

    [Fact]
    public async Task DropFilesCommand_WhenStartedFromListManager_ShouldRequestListManagerAfterCompletion()
    {
        string[] paths = [@"C:\Drop\a.png"];
        var pathPicker = new Mock<IPathPickerService>();
        var analyzer = CreateAnalyzerReturning(paths[0]);
        var vm = CreateViewModel(pathPicker, analyzer);
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
        var analyzer = CreateAnalyzerReturning(paths[0]);
        var vm = CreateViewModel(pathPicker, analyzer);
        using var statusRequests = new StatusRequestRecorder();

        await vm.DropFilesCommand.ExecuteAsync(paths);

        Assert.Equal(
            [AppStatus.FileAdd, AppStatus.Idle],
            statusRequests.Requests);
    }

    [Fact]
    public async Task DropFilesCommand_WhenConverting_ShouldNotAnalyzeDroppedPaths()
    {
        string[] paths = [@"C:\Drop\a.png"];
        var pathPicker = new Mock<IPathPickerService>();
        var analyzer = CreateAnalyzerReturning(paths[0]);
        var vm = CreateViewModel(pathPicker, analyzer);

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

    private static FileInputViewModel CreateViewModel(
        Mock<IPathPickerService> pathPicker,
        Mock<IFileAnalyzerService> analyzer,
        Mock<ISnackbarService>? snackbar = null)
    {
        var language = new Mock<ILanguageService>();
        language.Setup(service => service.GetString(It.IsAny<string>())).Returns<string>(key => key);

        var fileList = new FileListViewModel(language.Object, NullLogger<FileListViewModel>.Instance);
        var sorting = new Mock<ISortingService>();
        sorting
            .Setup(service => service.Sort(It.IsAny<IEnumerable<FileItem>>(), It.IsAny<SortType>(), It.IsAny<bool>()))
            .Returns<IEnumerable<FileItem>, SortType, bool>((items, _, _) => items);
        var sortFilter = new SortFilterViewModel(
            NullLogger<SortFilterViewModel>.Instance,
            language.Object,
            fileList,
            sorting.Object,
            new Mock<IDialogService>().Object,
            new Mock<ISnackbarService>().Object);

        return new FileInputViewModel(
            NullLogger<FileInputViewModel>.Instance,
            language.Object,
            (snackbar ?? new Mock<ISnackbarService>()).Object,
            analyzer.Object,
            fileList,
            sortFilter,
            pathPicker.Object);
    }

    private static Mock<IFileAnalyzerService> CreateAnalyzerReturning(string path)
    {
        return CreateAnalyzerReturning(new FileProcessingResult
        {
            NewItems = [new FileItem { Path = path }]
        });
    }

    private static Mock<IFileAnalyzerService> CreateAnalyzerReturning(FileProcessingResult result)
    {
        var analyzer = new Mock<IFileAnalyzerService>();
        analyzer
            .Setup(service => service.ProcessPathsAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<IReadOnlySet<string>?>(),
                It.IsAny<IProgress<FileProcessingProgress>?>()))
            .ReturnsAsync(result);
        return analyzer;
    }

    private sealed class StatusRequestRecorder : IDisposable
    {
        public List<AppStatus> Requests { get; } = [];

        public StatusRequestRecorder()
        {
            WeakReferenceMessenger.Default.Register<AppStatusRequestMessage>(
                this,
                static (recipient, message) =>
                    ((StatusRequestRecorder)recipient).Requests.Add(message.NewStatus));
        }

        public void Dispose()
        {
            WeakReferenceMessenger.Default.UnregisterAll(this);
        }
    }
}

[CollectionDefinition("MessengerTests", DisableParallelization = true)]
public sealed class MessengerTestCollectionDefinition
{
}
