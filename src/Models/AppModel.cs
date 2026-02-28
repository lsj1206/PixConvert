using CommunityToolkit.Mvvm.Messaging.Messages;

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
    Processing
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
