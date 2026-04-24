using System;
using System.IO;

namespace PixConvert.Services;

public static class AppPaths
{
    public static string DataFolder =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(AppContext.BaseDirectory));

    public static string LogsFolder => Path.Combine(DataFolder, AppMetadata.LogsFolderName);

    public static string SettingsPath => Path.Combine(DataFolder, AppMetadata.SettingsFileName);

    public static string PresetsPath => Path.Combine(DataFolder, AppMetadata.PresetsFileName);

    public static string LogFilePath => Path.Combine(LogsFolder, $"{AppMetadata.LogFilePrefix}.txt");

    public static void EnsureDataFolder()
    {
        Directory.CreateDirectory(DataFolder);
    }

    public static void EnsureLogsFolder()
    {
        EnsureDataFolder();
        Directory.CreateDirectory(LogsFolder);
    }
}
