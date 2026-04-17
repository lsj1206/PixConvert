namespace PixConvert.Models;

/// <summary>
/// settings.json 파일에 저장될 앱 전역 설정 구조입니다.
/// </summary>
public class AppSettings
{
    /// <summary>마지막으로 선택된 언어 코드</summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>파일 삭제 시 확인 다이얼로그 표시 여부</summary>
    public bool ConfirmDeletion { get; set; } = true;
}
