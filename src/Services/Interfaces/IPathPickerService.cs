namespace PixConvert.Services;

/// <summary>
/// Windows 파일 및 폴더 선택 대화상자를 표시하는 서비스입니다.
/// </summary>
public interface IPathPickerService
{
    /// <summary>여러 파일을 선택하고, 취소 시 빈 배열을 반환합니다.</summary>
    string[] PickFiles(string title);

    /// <summary>여러 폴더를 선택하고, 취소 시 빈 배열을 반환합니다.</summary>
    string[] PickFolders(string title);

    /// <summary>단일 폴더를 선택하고, 취소 시 null을 반환합니다.</summary>
    string? PickFolder(string title);
}
