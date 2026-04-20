using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using PixConvert.ViewModels;

namespace PixConvert.Services;

/// <summary>
/// Controls snackbar state and animation timing.
/// </summary>
public class SnackbarService : ISnackbarService, IDisposable
{
    private readonly SnackbarViewModel _viewModel;
    private readonly ILogger<SnackbarService> _logger;
    private readonly ILanguageService _languageService;
    private readonly Func<Dispatcher?> _dispatcherProvider;
    private readonly object _sessionLock = new();
    private long _currentSessionId;
    private CancellationTokenSource? _sessionCts;
    private volatile bool _disposed;

    private const int AnimationGap = 50;

    public SnackbarService(SnackbarViewModel viewModel, ILogger<SnackbarService> logger, ILanguageService languageService)
        : this(viewModel, logger, languageService, () => Application.Current?.Dispatcher)
    {
    }

    internal SnackbarService(
        SnackbarViewModel viewModel,
        ILogger<SnackbarService> logger,
        ILanguageService languageService,
        Func<Dispatcher?> dispatcherProvider)
    {
        _viewModel = viewModel;
        _logger = logger;
        _languageService = languageService;
        _dispatcherProvider = dispatcherProvider;
    }

    /// <summary>
    /// Shows a timed snackbar. Fire-and-forget by design.
    /// </summary>
    public async void Show(string message, SnackbarType type = SnackbarType.Info, int durationMs = 3000)
    {
        Dispatcher? dispatcher = GetActiveDispatcher();
        if (dispatcher == null || !TryStartTimedSession(out long sessionId, out CancellationToken cts))
        {
            return;
        }

        try
        {
            await await dispatcher.InvokeAsync(async () =>
            {
                if (!IsSessionActive(sessionId) || IsDispatcherUnavailable(dispatcher))
                {
                    return;
                }

                if (_viewModel.IsVisible)
                {
                    _viewModel.IsAnimating = false;
                    await Task.Delay(AnimationGap);
                }

                if (!IsSessionActive(sessionId) || IsDispatcherUnavailable(dispatcher))
                {
                    return;
                }

                _viewModel.Message = message;
                _viewModel.Type = type;
                _viewModel.IsVisible = true;
                _viewModel.IsAnimating = true;
            });

            await Task.Delay(durationMs, cts);

            if (IsSessionActive(sessionId) && !IsDispatcherUnavailable(dispatcher))
            {
                await await dispatcher.InvokeAsync(async () =>
                {
                    if (!IsSessionActive(sessionId) || IsDispatcherUnavailable(dispatcher))
                    {
                        return;
                    }

                    _viewModel.IsAnimating = false;
                    await Task.Delay(400);

                    if (IsSessionActive(sessionId) && !IsDispatcherUnavailable(dispatcher))
                    {
                        _viewModel.IsVisible = false;
                    }
                });
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException) when (cts.IsCancellationRequested || IsExpectedLifecycleException(dispatcher))
        {
        }
        catch (InvalidOperationException) when (IsExpectedLifecycleException(dispatcher))
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, _languageService.GetString("Log_Snackbar_ShowError"));
        }
    }

    /// <summary>
    /// Shows a persistent progress snackbar. Fire-and-forget by design.
    /// </summary>
    public async void ShowProgress(string message)
    {
        Dispatcher? dispatcher = GetActiveDispatcher();
        if (dispatcher == null || !TryStartProgressSession())
        {
            return;
        }

        try
        {
            await dispatcher.InvokeAsync(() =>
            {
                if (_disposed || IsDispatcherUnavailable(dispatcher))
                {
                    return;
                }

                _viewModel.Message = message;
                _viewModel.Type = SnackbarType.Info;
                _viewModel.IsVisible = true;
                _viewModel.IsAnimating = true;
            });
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException) when (IsExpectedLifecycleException(dispatcher))
        {
        }
        catch (InvalidOperationException) when (IsExpectedLifecycleException(dispatcher))
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, _languageService.GetString("Log_Snackbar_ShowProgressError"));
        }
    }

    /// <summary>
    /// Updates the active progress snackbar if the current session is still valid.
    /// </summary>
    public void UpdateProgress(string message)
    {
        if (_disposed)
        {
            return;
        }

        Dispatcher? dispatcher = GetActiveDispatcher();
        if (dispatcher == null)
        {
            return;
        }

        long capturedId = Interlocked.Read(ref _currentSessionId);

        try
        {
            dispatcher.BeginInvoke(() =>
            {
                if (IsSessionActive(capturedId) && _viewModel.IsVisible)
                {
                    _viewModel.Message = message;
                }
            });
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException) when (IsExpectedLifecycleException(dispatcher))
        {
        }
        catch (InvalidOperationException) when (IsExpectedLifecycleException(dispatcher))
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, _languageService.GetString("Log_Snackbar_ShowProgressError"));
        }
    }

    public void Dispose()
    {
        lock (_sessionLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Interlocked.Increment(ref _currentSessionId);
            CancelAndDispose(_sessionCts);
            _sessionCts = null;
        }
    }

    private bool TryStartTimedSession(out long sessionId, out CancellationToken token)
    {
        var nextCts = new CancellationTokenSource();

        lock (_sessionLock)
        {
            if (_disposed)
            {
                nextCts.Dispose();
                sessionId = 0;
                token = CancellationToken.None;
                return false;
            }

            sessionId = Interlocked.Increment(ref _currentSessionId);
            CancelAndDispose(_sessionCts);
            _sessionCts = nextCts;
            token = nextCts.Token;
            return true;
        }
    }

    private bool TryStartProgressSession()
    {
        lock (_sessionLock)
        {
            if (_disposed)
            {
                return false;
            }

            Interlocked.Increment(ref _currentSessionId);
            CancelAndDispose(_sessionCts);
            _sessionCts = null;
            return true;
        }
    }

    private Dispatcher? GetActiveDispatcher()
    {
        try
        {
            Dispatcher? dispatcher = _dispatcherProvider();
            return dispatcher == null || IsDispatcherUnavailable(dispatcher) ? null : dispatcher;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private bool IsSessionActive(long sessionId) =>
        !_disposed && Interlocked.Read(ref _currentSessionId) == sessionId;

    private bool IsExpectedLifecycleException(Dispatcher dispatcher) =>
        _disposed || IsDispatcherUnavailable(dispatcher);

    private static bool IsDispatcherUnavailable(Dispatcher dispatcher) =>
        dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished;

    private static void CancelAndDispose(CancellationTokenSource? cts)
    {
        if (cts == null)
        {
            return;
        }

        try
        {
            cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
        finally
        {
            cts.Dispose();
        }
    }
}
