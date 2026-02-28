using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using PixConvert.Models;
using PixConvert.Services;

namespace PixConvert.ViewModels;

/// <summary>
/// 모든 ViewModel의 기본 클래스입니다.
/// 공통 서비스(언어, 로깅)와 전역 상태(AppStatus) 동기화 기능을 제공합니다.
/// </summary>
public abstract partial class ViewModelBase : ObservableObject, IRecipient<AppStatusChangedMessage>
{
    protected readonly ILanguageService _languageService;
    protected readonly ILogger _logger;

    [ObservableProperty]
    private AppStatus _currentStatus = AppStatus.Idle;

    /// <summary>
    /// 현재 앱이 작업 중인지 여부를 반환합니다.
    /// </summary>
    public bool IsBusy => CurrentStatus != AppStatus.Idle;

    protected ViewModelBase(ILanguageService languageService, ILogger logger)
    {
        _languageService = languageService;
        _logger = logger;

        // 상태 변경 메시지 수신 등록
        WeakReferenceMessenger.Default.Register(this);
    }

    /// <summary>
    /// 메인 뷰모델 등에서 방송한 상태 변경 메시지를 수신하여 로컬 상태를 동기화합니다.
    /// </summary>
    public void Receive(AppStatusChangedMessage message)
    {
        CurrentStatus = message.Value;
        OnPropertyChanged(nameof(IsBusy));
        // 명령들의 CanExecute 상태를 자동으로 갱신하도록 유도 (필요 시 하위에서 오버라이드 가능)
        OnStatusChanged(message.Value);
    }

    /// <summary>
    /// 상태 변경 시 추가 로직을 수행하기 위한 가상 메서드입니다.
    /// </summary>
    protected virtual void OnStatusChanged(AppStatus newStatus) { }

    /// <summary>
    /// 메인 뷰모델에 상태 변경을 요청합니다.
    /// </summary>
    protected void RequestStatus(AppStatus newStatus)
    {
        WeakReferenceMessenger.Default.Send(new AppStatusRequestMessage(newStatus));
    }

    /// <summary>
    /// 다국어 리소스에서 키에 해당하는 문자열을 가져옵니다.
    /// </summary>
    protected string GetString(string key) => _languageService.GetString(key);
}
