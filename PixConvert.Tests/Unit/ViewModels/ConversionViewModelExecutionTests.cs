using Moq;
using PixConvert.Models;
using PixConvert.Services;
using Xunit;

namespace PixConvert.Tests;

public class ConversionViewModelExecutionTests
{
    [Fact]
    public async Task ConvertFilesCommand_WhenParallelProvidersComplete_ShouldUpdateBindingsOnUiThread()
    {
        await ConversionViewModelTestHarness.RunOnStaDispatcherAsync(async () =>
        {
            int uiThreadId = Environment.CurrentManagedThreadId;
            var files = ConversionViewModelTestHarness.CreateFiles(6);
            var provider = new ConversionViewModelTestHarness.ScriptedProvider(async (file, token) =>
            {
                int delay = (7 - int.Parse(file.Name.Replace("file", string.Empty))) * 10;
                await Task.Delay(delay, token);
                return new ConversionResult(FileConvertStatus.Success, file.Path + ".out", 10);
            });
            var vm = ConversionViewModelTestHarness.CreateExecutionViewModel(files, provider);
            var violations = ConversionViewModelTestHarness.TrackBindingThreadViolations(vm, files, uiThreadId, out var progressValues);

            await vm.ConvertFilesCommand.ExecuteAsync(null).WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Empty(violations);
            Assert.All(files, file =>
            {
                Assert.Equal(FileConvertStatus.Success, file.Status);
                Assert.Equal(100, file.Progress);
                Assert.False(string.IsNullOrWhiteSpace(file.OutputPath));
            });
            Assert.Equal(files.Count, vm.ProcessedCount);
            Assert.Equal(files.Count, vm.TotalConvertCount);
            Assert.Equal(0, vm.FailCount);
            Assert.Equal(100, vm.ConvertProgressPercent);
            Assert.True(vm.IsConversionCompleted);
            ConversionViewModelTestHarness.AssertProgressDoesNotRegress(progressValues);
        });
    }

    [Fact]
    public async Task ConvertFilesCommand_WhenProviderFails_ShouldSetErrorAndFailCountOnUiThread()
    {
        await ConversionViewModelTestHarness.RunOnStaDispatcherAsync(async () =>
        {
            int uiThreadId = Environment.CurrentManagedThreadId;
            var files = ConversionViewModelTestHarness.CreateFiles(3);
            var provider = new ConversionViewModelTestHarness.ScriptedProvider(async (file, token) =>
            {
                await Task.Delay(10, token);
                if (file.Name == "file2")
                    throw new InvalidOperationException("fake failure");

                return new ConversionResult(FileConvertStatus.Success, file.Path + ".out", 10);
            });
            var vm = ConversionViewModelTestHarness.CreateExecutionViewModel(files, provider);
            var violations = ConversionViewModelTestHarness.TrackBindingThreadViolations(vm, files, uiThreadId, out _);

            await vm.ConvertFilesCommand.ExecuteAsync(null).WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Empty(violations);
            Assert.Equal(FileConvertStatus.Error, files.Single(file => file.Name == "file2").Status);
            Assert.Equal(2, files.Count(file => file.Status == FileConvertStatus.Success));
            Assert.Equal(1, vm.FailCount);
            Assert.Equal(files.Count, vm.ProcessedCount);
            Assert.Equal(100, vm.ConvertProgressPercent);
        });
    }

    [Fact]
    public async Task ConvertFilesCommand_WhenProviderFails_ShouldShowFailureSummaryWithoutExceptionDetails()
    {
        await ConversionViewModelTestHarness.RunOnStaDispatcherAsync(async () =>
        {
            var files = ConversionViewModelTestHarness.CreateFiles(3);
            var snackbar = new Mock<ISnackbarService>();
            var provider = new ConversionViewModelTestHarness.ScriptedProvider(async (file, token) =>
            {
                await Task.Delay(10, token);
                if (file.Name == "file2")
                    throw new InvalidOperationException("fake failure");

                return new ConversionResult(FileConvertStatus.Success, file.Path + ".out", 10);
            });
            var vm = ConversionViewModelTestHarness.CreateExecutionViewModel(files, provider, snackbar: snackbar);

            await vm.ConvertFilesCommand.ExecuteAsync(null).WaitAsync(TimeSpan.FromSeconds(10));

            snackbar.Verify(
                service => service.Show("Msg_OperationCompleteWithFailures", SnackbarType.Warning, It.IsAny<int>()),
                Times.Once);
            snackbar.Verify(
                service => service.Show(It.Is<string>(message => message.Contains("fake failure", StringComparison.OrdinalIgnoreCase)), It.IsAny<SnackbarType>(), It.IsAny<int>()),
                Times.Never);
        });
    }

    [Fact]
    public async Task ConvertFilesCommand_WhenProviderSkipsAndFails_ShouldShowSkippedAndFailureSummary()
    {
        await ConversionViewModelTestHarness.RunOnStaDispatcherAsync(async () =>
        {
            var files = ConversionViewModelTestHarness.CreateFiles(3);
            var snackbar = new Mock<ISnackbarService>();
            var provider = new ConversionViewModelTestHarness.ScriptedProvider(async (file, token) =>
            {
                await Task.Delay(10, token);
                return file.Name switch
                {
                    "file1" => new ConversionResult(FileConvertStatus.Success, file.Path + ".out", 10),
                    "file2" => new ConversionResult(FileConvertStatus.Skipped),
                    _ => throw new InvalidOperationException("fake failure")
                };
            });
            var vm = ConversionViewModelTestHarness.CreateExecutionViewModel(files, provider, snackbar: snackbar);

            await vm.ConvertFilesCommand.ExecuteAsync(null).WaitAsync(TimeSpan.FromSeconds(10));

            snackbar.Verify(
                service => service.Show("Msg_OperationCompleteWithSkippedAndFailures", SnackbarType.Warning, It.IsAny<int>()),
                Times.Once);
        });
    }

    [Fact]
    public async Task ConvertFilesCommand_WhenCancelled_ShouldRestorePendingOnUiThread()
    {
        await ConversionViewModelTestHarness.RunOnStaDispatcherAsync(async () =>
        {
            int uiThreadId = Environment.CurrentManagedThreadId;
            var files = ConversionViewModelTestHarness.CreateFiles(1);
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var provider = new ConversionViewModelTestHarness.ScriptedProvider(async (_, token) =>
            {
                started.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return new ConversionResult(FileConvertStatus.Success, "unused", 1);
            });
            var vm = ConversionViewModelTestHarness.CreateExecutionViewModel(files, provider, cancelConfirmed: true);
            var violations = ConversionViewModelTestHarness.TrackBindingThreadViolations(vm, files, uiThreadId, out _);

            var convertTask = vm.ConvertFilesCommand.ExecuteAsync(null);
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));

            vm.CurrentStatus = AppStatus.Converting;
            await vm.CancelConvertCommand.ExecuteAsync(null);
            await convertTask.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Empty(violations);
            Assert.Equal(FileConvertStatus.Pending, files[0].Status);
            Assert.Equal(0, vm.ProcessedCount);
            Assert.Equal(0, vm.TotalConvertCount);
            Assert.Equal(0, vm.ConvertProgressPercent);
            Assert.False(vm.IsConversionCompleted);
        });
    }
}
