using System.IO;
using PixConvert.Services;

namespace PixConvert.Tests;

public class AppPathsTests
{
    [Fact]
    public void Paths_ShouldComposeMetadataDrivenLocations()
    {
        string expectedAppData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppMetadata.AppDataFolderName);

        Assert.Equal(expectedAppData, AppPaths.AppDataFolder);
        Assert.Equal(Path.Combine(expectedAppData, AppMetadata.LogsFolderName), AppPaths.LogsFolder);
        Assert.Equal(Path.Combine(expectedAppData, AppMetadata.SettingsFileName), AppPaths.SettingsPath);
        Assert.Equal(Path.Combine(expectedAppData, AppMetadata.PresetsFileName), AppPaths.PresetsPath);
        Assert.Equal(
            Path.Combine(expectedAppData, AppMetadata.LogsFolderName, $"{AppMetadata.LogFilePrefix}.txt"),
            AppPaths.LogFilePath);
    }
}
