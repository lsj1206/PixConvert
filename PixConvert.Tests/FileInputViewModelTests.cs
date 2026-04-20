using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PixConvert.Models;
using PixConvert.Services;
using PixConvert.ViewModels;
using Xunit;

namespace PixConvert.Tests;

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
        Mock<IFileAnalyzerService> analyzer)
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
            new Mock<ISnackbarService>().Object,
            analyzer.Object,
            fileList,
            sortFilter,
            pathPicker.Object);
    }

    private static Mock<IFileAnalyzerService> CreateAnalyzerReturning(string path)
    {
        var analyzer = new Mock<IFileAnalyzerService>();
        analyzer
            .Setup(service => service.ProcessPathsAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<IReadOnlySet<string>?>(),
                It.IsAny<IProgress<FileProcessingProgress>?>()))
            .ReturnsAsync(new FileProcessingResult
            {
                NewItems = [new FileItem { Path = path }]
            });
        return analyzer;
    }
}
