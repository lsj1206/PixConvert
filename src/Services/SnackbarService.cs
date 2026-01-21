using System;
using System.Threading.Tasks;
using System.Windows.Threading;
using PixConvert.ViewModels;

namespace PixConvert.Services;

/// <summary>
/// 스낵바 알림의 상태와 애니메이션 시간을 제어하는 서비스 클래스입니다.
/// </summary>
public class SnackbarService : ISnackbarService
{
    private readonly SnackbarViewModel _viewModel;
    private DispatcherTimer? _timer;
    private bool _isTransitioning;

    /// <summary>
    /// SnackbarService의 새 인스턴스를 초기화합니다.
    /// </summary>
    /// <param name="viewModel">상태를 바인딩할 뷰모델</param>
    public SnackbarService(SnackbarViewModel viewModel)
    {
        _viewModel = viewModel;
    }

    /// <summary>
    /// 메시지를 화면에 표시하고 지정된 시간 후 자동으로 닫습니다.
    /// </summary>
    /// <param name="message">알림 내용</param>
    /// <param name="type">알림 성격 (아이콘/색상 결정)</param>
    /// <param name="durationMs">유지 시간</param>
    public async void Show(string message, SnackbarType type = SnackbarType.Info, int durationMs = 3000)
    {
        // 애니메이션 전환 중 중복 호출 방지
        if (_isTransitioning) return;
        _isTransitioning = true;

        try
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                // 이미 다른 메시지가 표시 중인 경우 페이드 아웃 후 교체
                if (_viewModel.IsVisible && _viewModel.IsAnimating)
                {
                    _viewModel.IsAnimating = false;
                    _timer?.Stop();
                    await Task.Delay(350); // 애니메이션 지속 시간 대기
                    _viewModel.IsVisible = false;
                    await Task.Delay(50);
                }

                // 새로운 알림 정보 설정
                _viewModel.Message = message;
                _viewModel.Type = type;
                _viewModel.IsVisible = true;
                _viewModel.IsAnimating = true;

                // 자동 닫힘 타이머 시작
                _timer?.Stop();
                _timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(durationMs)
                };

                _timer.Tick += (s, e) =>
                {
                    _timer.Stop();
                    CloseInternal();
                };

                _timer.Start();
            });
        }
        finally
        {
            _isTransitioning = false;
        }
    }

    /// <summary>
    /// 장시간 작업 시 진행 상태를 알리기 위해 자동 닫힘이 비활성화된 알림을 표시합니다.
    /// </summary>
    /// <param name="message">수행 중인 작업 내용</param>
    public void ShowProgress(string message)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            _timer?.Stop(); // 자동 닫힘 방지

            _viewModel.Message = message;
            _viewModel.Type = SnackbarType.Info;
            _viewModel.IsVisible = true;
            _viewModel.IsAnimating = true;
        });
    }

    /// <summary>
    /// 활성화된 진행 알림의 텍스트만 실시간으로 업데이트합니다.
    /// </summary>
    /// <param name="message">갱신된 진행 상황</param>
    public void UpdateProgress(string message)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            if (_viewModel.IsVisible)
            {
                _viewModel.Message = message;
            }
        });
    }

    /// <summary>
    /// 스낵바를 페이드 아웃시키고 내부 상태를 정리합니다.
    /// </summary>
    private void CloseInternal()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(async () =>
        {
            _viewModel.IsAnimating = false;
            await Task.Delay(350); // 퇴장 애니메이션 시간 대기

            // 그 사이 새로운 애니메이션이 시작되지 않은 경우에만 완전히 숨김
            if (!_viewModel.IsAnimating)
            {
                _viewModel.IsVisible = false;
            }
        });
    }
}
