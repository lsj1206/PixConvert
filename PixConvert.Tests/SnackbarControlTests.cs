using System.ComponentModel;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using PixConvert.Views.Layouts;

namespace PixConvert.Tests;

public class SnackbarControlTests
{
    [Fact]
    public async Task DataContextChanged_WhenReplaced_ShouldUnsubscribeOldViewModel()
    {
        await RunOnStaDispatcherAsync(() =>
        {
            var control = new SnackbarControl();
            var oldViewModel = new ObservableTestViewModel();
            var newViewModel = new ObservableTestViewModel();

            control.DataContext = oldViewModel;
            Assert.Equal(1, oldViewModel.HandlerCount);

            control.DataContext = newViewModel;

            Assert.Equal(0, oldViewModel.HandlerCount);
            Assert.Equal(1, newViewModel.HandlerCount);

            oldViewModel.RaiseIsAnimatingChanged();
            newViewModel.RaiseIsAnimatingChanged();

            Assert.Equal(0, oldViewModel.DeliveredNotificationCount);
            Assert.Equal(1, newViewModel.DeliveredNotificationCount);
        });
    }

    [Fact]
    public async Task Unloaded_WhenDataContextIsObservable_ShouldUnsubscribeCurrentViewModel()
    {
        await RunOnStaDispatcherAsync(() =>
        {
            var control = new SnackbarControl();
            var viewModel = new ObservableTestViewModel();

            control.DataContext = viewModel;
            Assert.Equal(1, viewModel.HandlerCount);

            control.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));

            Assert.Equal(0, viewModel.HandlerCount);

            viewModel.RaiseIsAnimatingChanged();
            Assert.Equal(0, viewModel.DeliveredNotificationCount);
        });
    }

    private static async Task RunOnStaDispatcherAsync(Action action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));

            dispatcher.InvokeAsync(() =>
            {
                try
                {
                    action();
                    completion.SetResult();
                }
                catch (Exception ex)
                {
                    completion.SetException(ex);
                }
                finally
                {
                    dispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
                }
            });

            Dispatcher.Run();
        })
        {
            IsBackground = true
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        await completion.Task.WaitAsync(TimeSpan.FromSeconds(20));
        Assert.True(thread.Join(TimeSpan.FromSeconds(5)));
    }

    private sealed class ObservableTestViewModel : INotifyPropertyChanged
    {
        private PropertyChangedEventHandler? _propertyChanged;

        public int HandlerCount => _propertyChanged?.GetInvocationList().Length ?? 0;
        public int DeliveredNotificationCount { get; private set; }

        public event PropertyChangedEventHandler? PropertyChanged
        {
            add => _propertyChanged += value;
            remove => _propertyChanged -= value;
        }

        public void RaiseIsAnimatingChanged()
        {
            var handlers = _propertyChanged?.GetInvocationList();
            if (handlers == null)
            {
                return;
            }

            foreach (PropertyChangedEventHandler handler in handlers)
            {
                DeliveredNotificationCount++;
                handler(this, new PropertyChangedEventArgs("IsAnimating"));
            }
        }
    }
}
