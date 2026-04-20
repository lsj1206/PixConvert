using System;
using System.IO;
using System.Linq;
using Xunit;

namespace PixConvert.Tests;

public class MvvmBoundaryTests
{
    [Fact]
    public void Models_ShouldNotReferenceWpfApis()
    {
        string root = FindRepositoryRoot();
        string[] modelFiles = Directory.GetFiles(Path.Combine(root, "src", "Models"), "*.cs", SearchOption.AllDirectories);

        string[] violations = modelFiles
            .Where(file => File.ReadAllText(file).Contains("System.Windows", StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(root, file))
            .ToArray();

        Assert.True(violations.Length == 0, $"Models must not reference WPF APIs: {string.Join(", ", violations)}");
    }

    [Fact]
    public void SortFilterViewModel_ShouldNotOwnCollectionViewFiltering()
    {
        string root = FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(root, "src", "ViewModels", "SortFilterViewModel.cs"));

        Assert.DoesNotContain("CollectionViewSource", source, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Windows.Data", source, StringComparison.Ordinal);
    }

    [Fact]
    public void FileListControlCodeBehind_ShouldNotReachIntoMainViewModel()
    {
        string root = FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(root, "src", "Views", "Lists", "FileListControl.xaml.cs"));

        Assert.DoesNotContain("MainViewModel", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Window.GetWindow", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DataContext is", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ConvertingListControl_ShouldNotBindModelStatusText()
    {
        string root = FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(root, "src", "Views", "Lists", "ConvertingListControl.xaml"));

        Assert.DoesNotContain("StatusText", source, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src")) &&
                Directory.Exists(Path.Combine(directory.FullName, "PixConvert.Tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
