using System;
using System.IO;

namespace PixConvert.Services;

public static class AppPaths
{
    public static string AppDataFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PixConvert");

    public static string LogsFolder => Path.Combine(AppDataFolder, "logs");

    public static void EnsureAppDataFolder()
    {
        Directory.CreateDirectory(AppDataFolder);
    }

    public static void EnsureLogsFolder()
    {
        Directory.CreateDirectory(LogsFolder);
    }
}
