using System.Diagnostics;
using System.IO;
using PixConvert.Services.Interfaces;

namespace PixConvert.Services;

public sealed class ExternalLauncher : IExternalLauncher
{
    public void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    public void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }
}
