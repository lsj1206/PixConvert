using Microsoft.Extensions.Logging;
using PixConvert.Models;
using PixConvert.Services;
using PixConvert.Services.Interfaces;
using PixConvert.Services.Providers;
using PixConvert.ViewModels;
using Moq;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Threading;
using System.Windows.Threading;
using Xunit;

namespace PixConvert.Tests;

public class ConversionViewModelTests
{
    private readonly Mock<IPresetService> _mockPreset;
    private readonly Mock<IDialogService> _mockDialog;
    private readonly FileListViewModel _fileListVm;
    private readonly ConversionViewModel _vm;

    public ConversionViewModelTests()
    {
        var mockLang = new Mock<ILanguageService>();
        var mockLogger = new Mock<ILogger<ConversionViewModel>>();
        _mockPreset = new Mock<IPresetService>();
        var mockSnackbar = new Mock<ISnackbarService>();
        _mockDialog = new Mock<IDialogService>();

        mockLang.Setup(l => l.GetString(It.IsAny<string>())).Returns<string>(s => s);
        var presetConfig = new PresetConfig();
        presetConfig.Presets.Add(new ConvertPreset { Name = "Default", Settings = new ConvertSettings() });
        _mockPreset.Setup(p => p.Config).Returns(presetConfig);
        _mockPreset.Setup(p => p.ActivePreset).Returns((ConvertPreset?)null);
        _mockPreset.Setup(p => p.SaveAsync()).ReturnsAsync(true);

        var mockSkia = new Mock<SkiaSharpProvider>(mockLang.Object, new Mock<ILogger<SkiaSharpProvider>>().Object);
        var mockVips = new Mock<NetVipsProvider>(mockLang.Object, new Mock<ILogger<NetVipsProvider>>().Object);
        mockSkia.As<IProviderService>().Setup(p => p.Name).Returns("SkiaSharp");
        mockVips.As<IProviderService>().Setup(p => p.Name).Returns("NetVips");
        var engineSelector = new EngineSelector(mockSkia.Object, mockVips.Object);

        _fileListVm = new FileListViewModel(mockLang.Object, new Mock<ILogger<FileListViewModel>>().Object);

        _vm = new ConversionViewModel(
            mockLogger.Object,
            mockLang.Object,
            _mockDialog.Object,
            mockSnackbar.Object,
            _mockPreset.Object,
            _fileListVm,
            engineSelector,
            () => CreateConvertSettingViewModel(mockLang));
    }

    [Fact]
    public void DefaultState_ShouldInitializeAsEmptyPreset()
    {
        Assert.Equal("Converting_SelectPreset", _vm.ActivePresetName);
        Assert.False(_vm.IsActivePresetValid);
    }

    [Fact]
    public void Items_ShouldExposeUnderlyingFileListItems()
    {
        _fileListVm.AddItem(new FileItem { Path = @"C:\test.png" });

        Assert.Single(_vm.Items);
        Assert.Equal(@"C:\test.png", _vm.Items[0].Path);
    }

    [Fact]
    public void HasFailures_ShouldTrackFailCount()
    {
        Assert.False(_vm.HasFailures);

        _vm.FailCount = 1;

        Assert.True(_vm.HasFailures);
    }

    [Fact]
    public void Commands_WhenConverting_ShouldDisableStartAndEnableCancel()
    {
        _vm.CurrentStatus = AppStatus.Converting;

        Assert.False(_vm.OpenConvertSettingCommand.CanExecute(null));
        Assert.False(_vm.ConvertFilesCommand.CanExecute(null));
        Assert.True(_vm.CancelConvertCommand.CanExecute(null));
    }

    [Fact]
    public async Task OpenConvertSettingCommand_WhenConfirmed_ShouldSavePreset()
    {
        _mockDialog
            .Setup(service => service.ShowConvertSettingDialogAsync(It.IsAny<ConvertSettingViewModel>()))
            .ReturnsAsync(true);

        await _vm.OpenConvertSettingCommand.ExecuteAsync(null);

        _mockDialog.Verify(
            service => service.ShowConvertSettingDialogAsync(It.IsAny<ConvertSettingViewModel>()),
            Times.Once);
        _mockPreset.Verify(service => service.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task ConvertFilesCommand_WhenParallelProvidersComplete_ShouldUpdateBindingsOnUiThread()
    {
        await RunOnStaDispatcherAsync(async () =>
        {
            int uiThreadId = Environment.CurrentManagedThreadId;
            var files = CreateFiles(6);
            var provider = new ScriptedProvider(async (file, token) =>
            {
                int delay = (7 - int.Parse(file.Name.Replace("file", string.Empty))) * 10;
                await Task.Delay(delay, token);
                return new ConversionResult(FileConvertStatus.Success, file.Path + ".out", 10);
            });
            var vm = CreateConversionViewModel(files, provider);
            var violations = TrackBindingThreadViolations(vm, files, uiThreadId, out var progressValues);

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
            AssertProgressDoesNotRegress(progressValues);
        });
    }

    [Fact]
    public async Task ConvertFilesCommand_WhenProviderFails_ShouldSetErrorAndFailCountOnUiThread()
    {
        await RunOnStaDispatcherAsync(async () =>
        {
            int uiThreadId = Environment.CurrentManagedThreadId;
            var files = CreateFiles(3);
            var provider = new ScriptedProvider(async (file, token) =>
            {
                await Task.Delay(10, token);
                if (file.Name == "file2")
                    throw new InvalidOperationException("fake failure");

                return new ConversionResult(FileConvertStatus.Success, file.Path + ".out", 10);
            });
            var vm = CreateConversionViewModel(files, provider);
            var violations = TrackBindingThreadViolations(vm, files, uiThreadId, out _);

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
        await RunOnStaDispatcherAsync(async () =>
        {
            var files = CreateFiles(3);
            var snackbar = new Mock<ISnackbarService>();
            var provider = new ScriptedProvider(async (file, token) =>
            {
                await Task.Delay(10, token);
                if (file.Name == "file2")
                    throw new InvalidOperationException("fake failure");

                return new ConversionResult(FileConvertStatus.Success, file.Path + ".out", 10);
            });
            var vm = CreateConversionViewModel(files, provider, snackbar: snackbar);

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
        await RunOnStaDispatcherAsync(async () =>
        {
            var files = CreateFiles(3);
            var snackbar = new Mock<ISnackbarService>();
            var provider = new ScriptedProvider(async (file, token) =>
            {
                await Task.Delay(10, token);
                return file.Name switch
                {
                    "file1" => new ConversionResult(FileConvertStatus.Success, file.Path + ".out", 10),
                    "file2" => new ConversionResult(FileConvertStatus.Skipped),
                    _ => throw new InvalidOperationException("fake failure")
                };
            });
            var vm = CreateConversionViewModel(files, provider, snackbar: snackbar);

            await vm.ConvertFilesCommand.ExecuteAsync(null).WaitAsync(TimeSpan.FromSeconds(10));

            snackbar.Verify(
                service => service.Show("Msg_OperationCompleteWithSkippedAndFailures", SnackbarType.Warning, It.IsAny<int>()),
                Times.Once);
        });
    }

    [Fact]
    public async Task ConvertFilesCommand_WhenCancelled_ShouldRestorePendingOnUiThread()
    {
        await RunOnStaDispatcherAsync(async () =>
        {
            int uiThreadId = Environment.CurrentManagedThreadId;
            var files = CreateFiles(1);
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var provider = new ScriptedProvider(async (_, token) =>
            {
                started.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return new ConversionResult(FileConvertStatus.Success, "unused", 1);
            });
            var vm = CreateConversionViewModel(files, provider, cancelConfirmed: true);
            var violations = TrackBindingThreadViolations(vm, files, uiThreadId, out _);

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

    [Fact]
    public void BuildStandardOptionsSummary_WhenJpeg_ShouldIncludeQualityChromaAndBackground()
    {
        var settings = new ConvertSettings
        {
            StandardTargetFormat = "JPEG",
            StandardQuality = 90,
            StandardJpegChromaSubsampling = JpegChromaSubsamplingMode.Chroma444,
            StandardBackgroundColor = "#101010"
        };
        var files = new List<FileItem> { new() { Path = @"C:\test.png", FileSignature = "PNG" } };

        string summary = ConversionSummaryBuilder.BuildStandardOptionsSummary(settings, files, Key);

        Assert.Equal(
            string.Join(Environment.NewLine,
                "Setting_Quality 90",
                "Setting_ChromaSubsampling Setting_Subsampling_444",
                "Converting_BgColor #101010"),
            summary);
    }

    [Fact]
    public void BuildStandardOptionsSummary_WhenJpeg422WithAvifInput_ShouldShowNetVipsFallback()
    {
        var settings = new ConvertSettings
        {
            StandardTargetFormat = "JPEG",
            StandardQuality = 90,
            StandardJpegChromaSubsampling = JpegChromaSubsamplingMode.Chroma422,
            StandardBackgroundColor = "#FFFFFF"
        };
        var files = new List<FileItem> { new() { Path = @"C:\test.avif", FileSignature = "AVIF" } };

        string summary = ConversionSummaryBuilder.BuildStandardOptionsSummary(settings, files, Key);

        Assert.Contains("Setting_ChromaSubsampling Converting_Jpeg422AvifAuto", summary);
    }

    [Fact]
    public void BuildStandardOptionsSummary_WhenAvifLossless_ShouldExcludeQualityAndChroma()
    {
        var settings = new ConvertSettings
        {
            StandardTargetFormat = "AVIF",
            StandardLossless = true,
            StandardQuality = 90,
            StandardAvifEncodingEffort = 9,
            StandardAvifBitDepth = AvifBitDepthMode.Bit10
        };
        var files = new List<FileItem> { new() { Path = @"C:\test.png", FileSignature = "PNG" } };

        string summary = ConversionSummaryBuilder.BuildStandardOptionsSummary(settings, files, Key);

        Assert.Equal(
            string.Join(Environment.NewLine,
                "Setting_Lossless",
                "Setting_EncodingEffort 9",
                "Setting_BitDepth Setting_BitDepth_10"),
            summary);
    }

    [Fact]
    public void BuildAnimationOptionsSummary_WhenWebpLossy_ShouldIncludeQualityEffortAndPreset()
    {
        var settings = new ConvertSettings
        {
            AnimationTargetFormat = "WEBP",
            AnimationLossless = false,
            AnimationQuality = 80,
            AnimationWebpEncodingEffort = 6,
            AnimationWebpPreset = WebpPresetMode.Photo
        };
        var files = new List<FileItem> { new() { Path = @"C:\test.gif", FileSignature = "GIF", IsAnimation = true } };

        string summary = ConversionSummaryBuilder.BuildAnimationOptionsSummary(settings, files, Key);

        Assert.Equal(
            string.Join(Environment.NewLine,
                "Setting_Quality 80",
                "Setting_EncodingEffort 6",
                "Setting_WebpPreset Setting_WebpPreset_Photo"),
            summary);
    }

    [Fact]
    public void BuildAnimationOptionsSummary_WhenGif_ShouldIncludePaletteAndErrors()
    {
        var settings = new ConvertSettings
        {
            AnimationTargetFormat = "GIF",
            AnimationGifPalettePreset = GifPalettePreset.Balance,
            AnimationGifEncodingEffort = 9,
            AnimationGifInterframeMaxError = 1.25,
            AnimationGifInterpaletteMaxError = 2.5
        };
        var files = new List<FileItem> { new() { Path = @"C:\test.gif", FileSignature = "GIF", IsAnimation = true } };

        string summary = ConversionSummaryBuilder.BuildAnimationOptionsSummary(settings, files, Key);

        Assert.Equal(
            string.Join(Environment.NewLine,
                "Setting_PalettePreset Setting_GifPalette_Balance",
                "Setting_EncodingEffort 9",
                "Setting_InterframeMaxError 1.25",
                "Setting_InterpaletteMaxError 2.5"),
            summary);
    }

    [Fact]
    public void BuildAnimationOptionsSummary_WhenAnimationTargetIsNull_ShouldReturnEmpty()
    {
        var settings = new ConvertSettings { AnimationTargetFormat = null };
        var files = new List<FileItem> { new() { Path = @"C:\test.gif", FileSignature = "GIF", IsAnimation = true } };

        string summary = ConversionSummaryBuilder.BuildAnimationOptionsSummary(settings, files, Key);

        Assert.Equal(string.Empty, summary);
    }

    private static string Key(string key) => key;

    private static async Task RunOnStaDispatcherAsync(Func<Task> action)
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
                    await action();
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

    private static List<FileItem> CreateFiles(int count)
    {
        return Enumerable.Range(1, count)
            .Select(i => new FileItem
            {
                Path = $@"C:\input\file{i}.png",
                FileSignature = "PNG",
                IsUnsupported = false
            })
            .ToList();
    }

    private static ConversionViewModel CreateConversionViewModel(
        IReadOnlyList<FileItem> files,
        IProviderService provider,
        bool cancelConfirmed = false,
        Mock<ISnackbarService>? snackbar = null)
    {
        var language = new Mock<ILanguageService>();
        var logger = new Mock<ILogger<ConversionViewModel>>();
        var dialog = new Mock<IDialogService>();
        snackbar ??= new Mock<ISnackbarService>();
        var presetService = new Mock<IPresetService>();
        var engineSelector = new FakeEngineSelector(provider);

        language.Setup(service => service.GetString(It.IsAny<string>())).Returns<string>(key => key);
        dialog
            .Setup(service => service.ShowConfirmationAsync(It.IsAny<string>(), "Dlg_Title_Convert", It.IsAny<string?>()))
            .ReturnsAsync(true);
        dialog
            .Setup(service => service.ShowConfirmationAsync(It.IsAny<string>(), "Dlg_Title_CancelConvert", It.IsAny<string?>()))
            .ReturnsAsync(cancelConfirmed);

        var settings = new ConvertSettings
        {
            StandardTargetFormat = "PNG",
            CpuUsage = CpuUsageOption.Max
        };
        var preset = new ConvertPreset { Name = "Default", Settings = settings };
        var config = new PresetConfig();
        config.Presets.Add(preset);
        presetService.Setup(service => service.Config).Returns(config);
        presetService.Setup(service => service.ActivePreset).Returns(preset);
        presetService.Setup(service => service.SaveAsync()).ReturnsAsync(true);
        string errorKey = string.Empty;
        presetService
            .Setup(service => service.ValidPresetData(It.IsAny<ConvertSettings>(), out errorKey))
            .Returns(true);

        var fileList = new FileListViewModel(language.Object, new Mock<ILogger<FileListViewModel>>().Object);
        foreach (var file in files)
        {
            fileList.AddItem(file);
        }

        return new ConversionViewModel(
            logger.Object,
            language.Object,
            dialog.Object,
            snackbar.Object,
            presetService.Object,
            fileList,
            engineSelector,
            () => new ConvertSettingViewModel(
                language.Object,
                new Mock<ILogger<ConvertSettingViewModel>>().Object,
                presetService.Object,
                new Mock<IPathPickerService>().Object));
    }

    private static ConcurrentQueue<string> TrackBindingThreadViolations(
        ConversionViewModel vm,
        IEnumerable<FileItem> files,
        int uiThreadId,
        out ConcurrentQueue<int> progressValues)
    {
        var violations = new ConcurrentQueue<string>();
        progressValues = new ConcurrentQueue<int>();
        var capturedProgressValues = progressValues;

        void Track(object? sender, PropertyChangedEventArgs args)
        {
            if (Environment.CurrentManagedThreadId != uiThreadId)
            {
                violations.Enqueue($"{sender?.GetType().Name}.{args.PropertyName}");
            }

            if (ReferenceEquals(sender, vm) && args.PropertyName == nameof(ConversionViewModel.ConvertProgressPercent))
            {
                capturedProgressValues.Enqueue(vm.ConvertProgressPercent);
            }
        }

        vm.PropertyChanged += Track;
        foreach (var file in files)
        {
            file.PropertyChanged += Track;
        }

        return violations;
    }

    private static void AssertProgressDoesNotRegress(IEnumerable<int> progressValues)
    {
        int previous = 0;
        foreach (int current in progressValues)
        {
            Assert.True(current >= previous, $"Progress regressed from {previous} to {current}.");
            previous = current;
        }
    }

    private sealed class FakeEngineSelector : IEngineSelector
    {
        private readonly IProviderService _provider;

        public FakeEngineSelector(IProviderService provider)
        {
            _provider = provider;
        }

        public IProviderService GetProvider(FileItem file, ConvertSettings settings) => _provider;
    }

    private sealed class ScriptedProvider : IProviderService
    {
        private readonly Func<FileItem, CancellationToken, Task<ConversionResult>> _convert;

        public ScriptedProvider(Func<FileItem, CancellationToken, Task<ConversionResult>> convert)
        {
            _convert = convert;
        }

        public string Name => "Fake";

        public Task<ConversionResult> ConvertAsync(
            FileItem file,
            ConvertSettings settings,
            ConversionSession session,
            CancellationToken token)
        {
            return _convert(file, token);
        }
    }

    private ConvertSettingViewModel CreateConvertSettingViewModel(Mock<ILanguageService> language)
    {
        return new ConvertSettingViewModel(
            language.Object,
            new Mock<ILogger<ConvertSettingViewModel>>().Object,
            _mockPreset.Object,
            new Mock<IPathPickerService>().Object);
    }
}
