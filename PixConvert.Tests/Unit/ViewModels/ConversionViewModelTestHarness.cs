using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using Moq;
using PixConvert.Models;
using PixConvert.Services;
using PixConvert.Services.Interfaces;
using PixConvert.Services.Providers;
using PixConvert.ViewModels;

namespace PixConvert.Tests;

internal sealed class ConversionViewModelContext
{
    public required Mock<IPresetService> PresetService { get; init; }
    public required Mock<IDialogService> DialogService { get; init; }
    public required Mock<ISnackbarService> SnackbarService { get; init; }
    public required FileListViewModel FileList { get; init; }
    public required ConversionViewModel ViewModel { get; init; }
}

internal static class ConversionViewModelTestHarness
{
    public static ConversionViewModelContext CreateDefaultContext()
    {
        var language = new Mock<ILanguageService>();
        language.Setup(service => service.GetString(It.IsAny<string>())).Returns<string>(key => key);

        var logger = new Mock<ILogger<ConversionViewModel>>();
        var presetService = new Mock<IPresetService>();
        var dialogService = new Mock<IDialogService>();
        var snackbar = new Mock<ISnackbarService>();

        var presetConfig = new PresetConfig();
        presetConfig.Presets.Add(new ConvertPreset { Name = "Default", Settings = new ConvertSettings() });
        presetService.Setup(service => service.Config).Returns(presetConfig);
        presetService.Setup(service => service.ActivePreset).Returns((ConvertPreset?)null);
        presetService.Setup(service => service.SaveAsync()).ReturnsAsync(true);

        var mockSkia = new Mock<SkiaSharpProvider>(language.Object, new Mock<ILogger<SkiaSharpProvider>>().Object);
        var mockVips = new Mock<NetVipsProvider>(language.Object, new Mock<ILogger<NetVipsProvider>>().Object);
        mockSkia.As<IProviderService>().Setup(provider => provider.Name).Returns("SkiaSharp");
        mockVips.As<IProviderService>().Setup(provider => provider.Name).Returns("NetVips");
        var engineSelector = new EngineSelector(mockSkia.Object, mockVips.Object);

        var fileList = new FileListViewModel(language.Object, new Mock<ILogger<FileListViewModel>>().Object);
        var viewModel = new ConversionViewModel(
            logger.Object,
            language.Object,
            dialogService.Object,
            snackbar.Object,
            presetService.Object,
            fileList,
            engineSelector,
            () => new ConvertSettingViewModel(
                language.Object,
                new Mock<ILogger<ConvertSettingViewModel>>().Object,
                presetService.Object,
                new Mock<IPathPickerService>().Object));

        return new ConversionViewModelContext
        {
            PresetService = presetService,
            DialogService = dialogService,
            SnackbarService = snackbar,
            FileList = fileList,
            ViewModel = viewModel
        };
    }

    public static ConversionViewModel CreateExecutionViewModel(
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

    public static async Task RunOnStaDispatcherAsync(Func<Task> action)
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

    public static List<FileItem> CreateFiles(int count)
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

    public static ConcurrentQueue<string> TrackBindingThreadViolations(
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

    public static void AssertProgressDoesNotRegress(IEnumerable<int> progressValues)
    {
        int previous = 0;
        foreach (int current in progressValues)
        {
            Assert.True(current >= previous, $"Progress regressed from {previous} to {current}.");
            previous = current;
        }
    }

    public static string Key(string key) => key;

    internal sealed class FakeEngineSelector : IEngineSelector
    {
        private readonly IProviderService _provider;

        public FakeEngineSelector(IProviderService provider)
        {
            _provider = provider;
        }

        public IProviderService GetProvider(FileItem file, ConvertSettings settings) => _provider;
    }

    internal sealed class ScriptedProvider : IProviderService
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
}
