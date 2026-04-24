namespace PixConvert.Tests;

public class AppMetadataTests
{
    [Fact]
    public void ResolveDisplayVersion_WhenInformationalVersionContainsBuildSuffix_ShouldTrimSuffix()
    {
        string result = AppMetadata.ResolveDisplayVersion("1.2.3+abc123", "1.2.3.0");

        Assert.Equal("1.2.3", result);
    }

    [Fact]
    public void ResolveDisplayVersion_WhenInformationalVersionMissing_ShouldUseAssemblyVersion()
    {
        string result = AppMetadata.ResolveDisplayVersion(null, "1.2.3.0");

        Assert.Equal("1.2.3.0", result);
    }

    [Fact]
    public void ResolveDisplayVersion_WhenAllSourcesMissing_ShouldReturnUnknown()
    {
        string result = AppMetadata.ResolveDisplayVersion(null, null);

        Assert.Equal("Unknown", result);
    }

    [Fact]
    public void GetMetadataValue_WhenAssemblyMetadataMissing_ShouldReturnFallback()
    {
        string result = AppMetadata.GetMetadataValue(typeof(object).Assembly, "MissingKey", "fallback");

        Assert.Equal("fallback", result);
    }

    [Fact]
    public void MetadataUrlsAndDefaults_ShouldReturnConfiguredValues()
    {
        Assert.Equal("https://github.com/lsj1206/PixConvert", AppMetadata.RepositoryUrl);
        Assert.Equal("https://api.github.com/repos/lsj1206/PixConvert/releases/latest", AppMetadata.LatestReleaseApiUrl);
        Assert.Equal("logs", AppMetadata.LogsFolderName);
        Assert.Equal("settings.json", AppMetadata.SettingsFileName);
        Assert.Equal("presets.json", AppMetadata.PresetsFileName);
        Assert.Equal("pixconvert_log_", AppMetadata.LogFilePrefix);
        Assert.Equal("PixConvert", AppMetadata.HttpUserAgent);
        Assert.Equal("PixConvert", AppMetadata.DefaultOutputSubFolderName);
    }
}
