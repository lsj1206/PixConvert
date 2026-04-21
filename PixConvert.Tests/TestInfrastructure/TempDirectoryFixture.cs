using System;
using System.IO;
using System.Threading;

namespace PixConvert.Tests;

internal sealed class TempDirectoryFixture : IDisposable
{
    public string RootPath { get; }

    public TempDirectoryFixture(string prefix)
    {
        RootPath = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(RootPath);
    }

    public string CreatePath(params string[] segments)
    {
        string path = RootPath;
        foreach (string segment in segments)
        {
            path = Path.Combine(path, segment);
        }

        return path;
    }

    public string EnsureDirectory(params string[] segments)
    {
        string path = CreatePath(segments);
        Directory.CreateDirectory(path);
        return path;
    }

    public void Dispose()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();

        if (!Directory.Exists(RootPath))
            return;

        try
        {
            Directory.Delete(RootPath, true);
        }
        catch (IOException)
        {
            Thread.Sleep(500);
            try
            {
                Directory.Delete(RootPath, true);
            }
            catch
            {
            }
        }
    }
}
