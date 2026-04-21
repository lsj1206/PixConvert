using System;
using System.IO;

namespace PixConvert.Services;

public static class AppPaths
{
    public static string AppDataFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppMetadata.AppDataFolderName);

    public static string LogsFolder => Path.Combine(AppDataFolder, AppMetadata.LogsFolderName);

    public static string SettingsPath => Path.Combine(AppDataFolder, AppMetadata.SettingsFileName);

    public static string PresetsPath => Path.Combine(AppDataFolder, AppMetadata.PresetsFileName);

    public static string LogFilePath => Path.Combine(LogsFolder, $"{AppMetadata.LogFilePrefix}.txt");

    public static void EnsureAppDataFolder()
    {
        Directory.CreateDirectory(AppDataFolder);
    }

    public static void EnsureLogsFolder()
    {
        Directory.CreateDirectory(LogsFolder);
    }
}
