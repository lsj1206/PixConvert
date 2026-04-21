using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PixConvert.Models;
using PixConvert.Services;
using PixConvert.ViewModels;

namespace PixConvert.Tests;

internal static class FileInputViewModelTestFactory
{
    public static FileInputViewModel CreateViewModel(
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

    public static Mock<IFileAnalyzerService> CreateAnalyzerReturning(string path) =>
        CreateAnalyzerReturning(new FileProcessingResult
        {
            NewItems = [new FileItem { Path = path }]
        });

    public static Mock<IFileAnalyzerService> CreateAnalyzerReturning(FileProcessingResult result)
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
}
