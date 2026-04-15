using System;
using System.IO;

namespace PixConvert.Services;

public static class AppPaths
{
    public static string AppDataFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PixConvert");

    public static void EnsureAppDataFolder()
    {
        Directory.CreateDirectory(AppDataFolder);
    }
}
