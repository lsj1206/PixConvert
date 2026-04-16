using Microsoft.Win32;

namespace PixConvert.Services;

/// <summary>
/// Microsoft.Win32 대화상자를 통해 파일 및 폴더 경로를 선택합니다.
/// </summary>
public sealed class PathPickerService : IPathPickerService
{
    public string[] PickFiles(string title)
    {
        var dialog = new OpenFileDialog
        {
            Multiselect = true,
            Title = title
        };

        return dialog.ShowDialog() == true ? dialog.FileNames : Array.Empty<string>();
    }

    public string[] PickFolders(string title)
    {
        var dialog = new OpenFolderDialog
        {
            Multiselect = true,
            Title = title
        };

        return dialog.ShowDialog() == true ? dialog.FolderNames : Array.Empty<string>();
    }

    public string? PickFolder(string title)
    {
        var dialog = new OpenFolderDialog
        {
            Title = title
        };

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
}
