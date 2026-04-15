namespace PixConvert.Services.Interfaces;

public interface IExternalLauncher
{
    void OpenUrl(string url);

    void OpenFolder(string path);
}
