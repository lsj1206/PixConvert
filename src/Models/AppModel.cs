using CommunityToolkit.Mvvm.Messaging.Messages;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PixConvert.Models;

/// <summary>
/// 애플리케이션의 현재 작업 상태를 나타내는 열거형입니다.
/// </summary>
public enum AppStatus
{
    /// <summary>대기 상태</summary>
    Idle,
    /// <summary>파일 또는 폴더 추가 중</summary>
    FileAdd,
    /// <summary>파일 변환 진행 중</summary>
    Converting,
    /// <summary>일반적인 짧은 처리 중 (삭제, 비우기, 순번 재정렬 등)</summary>
    Processing,
    /// <summary>목록 설정 모드 (사이드바 전환 후 확장자/미지원 파일 제거 편집 상태)</summary>
    ListManager
}

/// <summary>
/// 언어 선택을 위한 옵션 정보를 담는 클래스입니다.
/// </summary>
public class LanguageOption
{
    /// <summary>화면에 표시될 이름 (예: EN, KR)</summary>
    public string Display { get; set; } = string.Empty;
    /// <summary>언어 코드 (예: en-US, ko-KR)</summary>
    public string Code { get; set; } = string.Empty;
}

/// <summary>
/// 애플리케이션 상태 변경을 요청할 때 사용하는 메시지 (서브 뷰모델 -> 메인 뷰모델)
/// </summary>
public class AppStatusRequestMessage : RequestMessage<AppStatus>
{
    public AppStatus NewStatus { get; }
    public AppStatusRequestMessage(AppStatus status) => NewStatus = status;
}

/// <summary>
/// 애플리케이션 상태가 실제로 변경되었음을 알리는 메시지 (메인 뷰모델 -> 모든 뷰모델)
/// </summary>
public class AppStatusChangedMessage : ValueChangedMessage<AppStatus>
{
    public AppStatusChangedMessage(AppStatus status) : base(status) { }
}

/// <summary>
/// 변환 주기 중 파일 진행 상태(현재 처리 중 파일명, 전체 퍼센트 등)를 전달하는 메시지입니다.
/// </summary>
public class ConvertProgressMessage
{
    public string FileName { get; init; } = string.Empty;
    public int ProcessedCount { get; init; }
    public int TotalCount { get; init; }
    public int FailCount { get; init; }
    public string PresetName { get; init; } = string.Empty;
}

/// <summary>
/// 이미지 변환 설정을 이름과 함께 저장하는 프리셋 클래스입니다.
/// </summary>
public partial class ConvertPreset : ObservableObject
{
    [ObservableProperty]
    private string name = "Preset";

    public ConvertSettings Settings { get; set; } = new();
}

/// <summary>
/// settings.json 파일에 저장될 전체 설정 구조입니다.
/// </summary>
public class PresetConfig
{
    /// <summary>마지막으로 사용된 프리셋의 이름 (또는 현재 상태)</summary>
    public string LastSelectedPresetName { get; set; } = string.Empty;

    /// <summary>사용자가 정의한 프리셋 목록</summary>
    public List<ConvertPreset> Presets { get; set; } = new();
}

