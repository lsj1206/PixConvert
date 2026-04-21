using System.Threading;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using Moq;
using PixConvert.Services;
using PixConvert.ViewModels;

namespace PixConvert.Tests;

public class SnackbarServiceTests
{
    [Fact]
    public async Task Calls_WhenDispatcherUnavailable_ShouldNoOp()
    {
        var (service, viewModel, logger) = CreateService(() => null);

        service.Show("Hello", SnackbarType.Success, 10);
        service.ShowProgress("Loading");
        service.UpdateProgress("Updated");

        await Task.Delay(50);

        Assert.Equal(string.Empty, viewModel.Message);
        Assert.False(viewModel.IsVisible);
        Assert.False(viewModel.IsAnimating);
        VerifyNoErrorLogs(logger);
    }

    [Fact]
    public async Task Calls_AfterDispose_ShouldNoOp()
    {
        await RunOnStaDispatcherAsync(async dispatcher =>
        {
            var (service, viewModel, logger) = CreateService(() => dispatcher);

            service.ShowProgress("Before");
            await WaitUntilAsync(() => viewModel.Message == "Before" && viewModel.IsVisible);

            service.Dispose();
            service.Show("After", SnackbarType.Error, 10);
            service.ShowProgress("AfterProgress");
            service.UpdateProgress("AfterUpdate");

            await Task.Delay(100);

            Assert.Equal("Before", viewModel.Message);
            Assert.Equal(SnackbarType.Info, viewModel.Type);
            Assert.True(viewModel.IsVisible);
            VerifyNoErrorLogs(logger);
        });
    }

    [Fact]
    public async Task Show_WhenDisposedAfterStart_ShouldCancelWithoutErrorLog()
    {
        await RunOnStaDispatcherAsync(async dispatcher =>
        {
            var (service, viewModel, logger) = CreateService(() => dispatcher);

            service.Show("Cancelable", SnackbarType.Warning, 5000);
            await WaitUntilAsync(() => viewModel.Message == "Cancelable" && viewModel.IsVisible);

            service.Dispose();
            await Task.Delay(100);

            VerifyNoErrorLogs(logger);
        });
    }

    [Fact]
    public async Task ShowAndProgress_WithActiveDispatcher_ShouldUpdateViewModel()
    {
        await RunOnStaDispatcherAsync(async dispatcher =>
        {
            var (service, viewModel, logger) = CreateService(() => dispatcher);

            service.Show("Saved", SnackbarType.Success, 5000);
            await WaitUntilAsync(() => viewModel.Message == "Saved" && viewModel.IsVisible);

            Assert.Equal(SnackbarType.Success, viewModel.Type);
            Assert.True(viewModel.IsAnimating);

            service.ShowProgress("Loading");
            await WaitUntilAsync(() => viewModel.Message == "Loading" && viewModel.IsVisible);

            Assert.Equal(SnackbarType.Info, viewModel.Type);
            Assert.True(viewModel.IsAnimating);

            service.UpdateProgress("Loading 2");
            await WaitUntilAsync(() => viewModel.Message == "Loading 2");

            service.Dispose();
            VerifyNoErrorLogs(logger);
        });
    }

    private static (SnackbarService Service, SnackbarViewModel ViewModel, Mock<ILogger<SnackbarService>> Logger) CreateService(
        Func<Dispatcher?> dispatcherProvider)
    {
        var language = new Mock<ILanguageService>();
        language.Setup(service => service.GetString(It.IsAny<string>())).Returns<string>(key => key);

        var viewModel = new SnackbarViewModel(
            language.Object,
            new Mock<ILogger<SnackbarViewModel>>().Object);

        var logger = new Mock<ILogger<SnackbarService>>();
        var service = new SnackbarService(viewModel, logger.Object, language.Object, dispatcherProvider);

        return (service, viewModel, logger);
    }

    private static async Task RunOnStaDispatcherAsync(Func<Dispatcher, Task> action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));

            dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    await action(dispatcher);
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

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);

        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("Condition was not met before the timeout.");
            }

            await Task.Delay(10);
        }
    }

    private static void VerifyNoErrorLogs(Mock<ILogger<SnackbarService>> logger)
    {
        logger.Verify(
            service => service.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }
}
