using System.IO;
using PixConvert.Services;

namespace PixConvert.Tests;

public class AppPathsTests
{
    [Fact]
    public void Paths_ShouldComposeMetadataDrivenLocations()
    {
        string expectedDataFolder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(AppContext.BaseDirectory));

        Assert.Equal(expectedDataFolder, AppPaths.DataFolder);
        Assert.Equal(Path.Combine(expectedDataFolder, AppMetadata.LogsFolderName), AppPaths.LogsFolder);
        Assert.Equal(Path.Combine(expectedDataFolder, AppMetadata.SettingsFileName), AppPaths.SettingsPath);
        Assert.Equal(Path.Combine(expectedDataFolder, AppMetadata.PresetsFileName), AppPaths.PresetsPath);
        Assert.Equal(
            Path.Combine(expectedDataFolder, AppMetadata.LogsFolderName, $"{AppMetadata.LogFilePrefix}.txt"),
            AppPaths.LogFilePath);
    }
}
