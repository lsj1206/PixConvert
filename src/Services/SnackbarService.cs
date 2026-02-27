using System;
using System.Windows;
using System.Threading;
using System.Threading.Tasks;
using PixConvert.ViewModels;

namespace PixConvert.Services;

/// <summary>
/// 스낵바 알림의 상태와 애니메이션 시간을 제어하는 서비스 클래스입니다.
/// </summary>
public class SnackbarService : ISnackbarService
{
    private readonly SnackbarViewModel _viewModel;
    private long _currentSessionId; // 현재 활성화된 알림 세션 번호
    private CancellationTokenSource? _sessionCts;

    // 애니메이션 지속 시간 (XAML Storyboard 시간과 조율)
    private const int AnimationGap = 50;

    public SnackbarService(SnackbarViewModel viewModel)
    {
        _viewModel = viewModel;
    }

    /// <summary>
    /// 새로운 메시지를 표시합니다. 진행 중인 다른 알림이 있다면 즉시 교체합니다.
    /// </summary>
    public async void Show(string message, SnackbarType type = SnackbarType.Info, int durationMs = 3000)
    {
        try
        {
            // 1. 새로운 세션 시작 및 이전 작업 취소
            long sessionId = Interlocked.Increment(ref _currentSessionId);
            _sessionCts?.Cancel();
            _sessionCts = new CancellationTokenSource();
            var cts = _sessionCts.Token;

            // 2. UI 상태를 원자적으로(Atomically) 변경
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                if (_viewModel.IsVisible)
                {
                    // 부드러운 교체를 위해 아주 짧은 지연 후 데이터 갱신
                    _viewModel.IsAnimating = false;
                    await Task.Delay(AnimationGap);
                }

                _viewModel.Message = message;
                _viewModel.Type = type;
                _viewModel.IsVisible = true;
                _viewModel.IsAnimating = true;
            });

            // 3. 지정된 시간 동안 대기 (새 알림이 오면 여기서 취소됨)
            await Task.Delay(durationMs, cts);

            // 4. 대기 후 내가 여전히 최신 알림인지 확인하고 퇴장
            if (Interlocked.Read(ref _currentSessionId) == sessionId)
            {
                await Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    // 마지막 확인 (Dispatcher 큐 진입 시간차 고려)
                    if (Interlocked.Read(ref _currentSessionId) == sessionId)
                    {
                        _viewModel.IsAnimating = false;
                        await Task.Delay(400); // 퇴장 애니메이션 대기

                        if (Interlocked.Read(ref _currentSessionId) == sessionId)
                            _viewModel.IsVisible = false;
                    }
                });
            }
        }
        catch (OperationCanceledException) { /* 신규 알림에 밀려남 */ }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Snackbar_Show_Error: {ex.Message}");
        }
    }

    /// <summary>
    /// 진행률 표시 모드를 시작합니다.
    /// </summary>
    public async void ShowProgress(string message)
    {
        try
        {
            // 세션을 갱신하여 이전의 모든 비동기 업데이트 무효화
            Interlocked.Increment(ref _currentSessionId);
            _sessionCts?.Cancel();
            _sessionCts = null; // 진행률은 명시적 종료까지 계속 유지됨

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                _viewModel.Message = message;
                _viewModel.Type = SnackbarType.Info;
                _viewModel.IsVisible = true;
                _viewModel.IsAnimating = true;
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Snackbar_ShowProgress_Error: {ex.Message}");
        }
    }

    /// <summary>
    /// 현재 세션이 유지되고 있을 때만 메시지를 업데이트합니다.
    /// </summary>
    public void UpdateProgress(string message)
    {
        // 호출 시점의 ID 캡처
        long capturedId = Interlocked.Read(ref _currentSessionId);

        // Dispatcher가 바빠서 늦게 실행되더라도, ID를 비교하여 과거 유령 데이터면 무시함
        Application.Current?.Dispatcher?.BeginInvoke(() =>
        {
            if (Interlocked.Read(ref _currentSessionId) == capturedId && _viewModel.IsVisible)
            {
                _viewModel.Message = message;
            }
        });
    }
}
