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
        string root = RepositoryRootHelper.FindRepositoryRoot();
        string[] modelFiles = Directory.GetFiles(Path.Combine(root, "src", "Models"), "*.cs", SearchOption.AllDirectories);

        string[] violations = modelFiles
            .Where(file => File.ReadAllText(file).Contains("System.Windows", StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(root, file))
            .ToArray();

        Assert.True(violations.Length == 0, $"Models must not reference WPF APIs: {string.Join(", ", violations)}");
    }

    [Fact]
    public void FileListControlCodeBehind_ShouldNotReachIntoMainViewModel()
    {
        string root = RepositoryRootHelper.FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(root, "src", "Views", "Lists", "FileListControl.xaml.cs"));

        Assert.DoesNotContain("MainViewModel", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Window.GetWindow", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DataContext is", source, StringComparison.Ordinal);
    }
}
