using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using CommunityToolkit.Mvvm.Messaging;
using Moq;
using PixConvert.Models;
using PixConvert.Services;
using PixConvert.Services.Interfaces;
using PixConvert.ViewModels;
using Xunit;

namespace PixConvert.Tests;

[Collection("MessengerIsolation")]
public sealed class MainViewModelTests
{
    [Fact]
    public void Receive_WhenStatusRequestIsHandled_ShouldUpdateCurrentStatus()
    {
        var context = CreateContext();

        context.ViewModel.Receive(new AppStatusRequestMessage(AppStatus.ListManager));

        Assert.Equal(AppStatus.ListManager, context.ViewModel.CurrentStatus);
    }

    [Fact]
    public void CurrentStatus_WhenChanged_ShouldBroadcastToChildViewModels()
    {
        var context = CreateContext();
        using var recorder = new AppStatusChangedRecorder();

        context.ViewModel.CurrentStatus = AppStatus.FileAdd;

        Assert.Contains(AppStatus.FileAdd, recorder.Statuses);
        Assert.Equal(AppStatus.FileAdd, context.Snackbar.CurrentStatus);
        Assert.Equal(AppStatus.FileAdd, context.SortFilter.CurrentStatus);
        Assert.Equal(AppStatus.FileAdd, context.FileInput.CurrentStatus);
        Assert.Equal(AppStatus.FileAdd, context.Conversion.CurrentStatus);
        Assert.Equal(AppStatus.FileAdd, context.ListManager.CurrentStatus);
        Assert.Equal(AppStatus.FileAdd, context.Header.CurrentStatus);
    }

    private static MainViewModelContext CreateContext()
    {
        var language = new Mock<ILanguageService>();
        language.Setup(service => service.GetString(It.IsAny<string>())).Returns<string>(key => key);
        language.Setup(service => service.GetCurrentLanguage()).Returns("ko-KR");

        var dialog = new Mock<IDialogService>();
        var snackbarService = new Mock<ISnackbarService>();
        var sortingService = new Mock<ISortingService>();
        sortingService
            .Setup(service => service.Sort(It.IsAny<IEnumerable<FileItem>>(), It.IsAny<SortType>(), It.IsAny<bool>()))
            .Returns<IEnumerable<FileItem>, SortType, bool>((items, _, _) => items);

        var fileList = new FileListViewModel(language.Object, NullLogger<FileListViewModel>.Instance);
        var snackbar = new SnackbarViewModel(language.Object, NullLogger<SnackbarViewModel>.Instance);
        var sortFilter = new SortFilterViewModel(
            NullLogger<SortFilterViewModel>.Instance,
            language.Object,
            fileList,
            sortingService.Object,
            dialog.Object,
            snackbarService.Object);

        var pathPicker = new Mock<IPathPickerService>();
        var analyzer = new Mock<IFileAnalyzerService>();
        var fileInput = new FileInputViewModel(
            NullLogger<FileInputViewModel>.Instance,
            language.Object,
            snackbarService.Object,
            analyzer.Object,
            fileList,
            sortFilter,
            pathPicker.Object);

        var presetService = new Mock<IPresetService>();
        var presetConfig = new PresetConfig();
        presetConfig.Presets.Add(new ConvertPreset { Name = "Default", Settings = new ConvertSettings() });
        presetService.SetupGet(service => service.Config).Returns(presetConfig);
        presetService.SetupGet(service => service.ActivePreset).Returns((ConvertPreset?)null);
        presetService.Setup(service => service.SaveAsync()).ReturnsAsync(true);

        var conversion = new ConversionViewModel(
            NullLogger<ConversionViewModel>.Instance,
            language.Object,
            dialog.Object,
            snackbarService.Object,
            presetService.Object,
            fileList,
            new ConversionViewModelTestHarness.FakeEngineSelector(
                new ConversionViewModelTestHarness.ScriptedProvider(
                    (file, token) => Task.FromResult(new ConversionResult(FileConvertStatus.Success, file.Path, 0)))),
            () => new ConvertSettingViewModel(
                language.Object,
                NullLogger<ConvertSettingViewModel>.Instance,
                presetService.Object,
                pathPicker.Object));

        var listManager = new ListManagerViewModel(
            NullLogger<ListManagerViewModel>.Instance,
            language.Object,
            snackbarService.Object,
            dialog.Object,
            fileList);

        var header = new HeaderViewModel(
            language.Object,
            NullLogger<HeaderViewModel>.Instance,
            fileList,
            dialog.Object,
            () => throw new InvalidOperationException("Settings dialog is not used in this test."));

        var viewModel = new MainViewModel(
            NullLogger<MainViewModel>.Instance,
            language.Object,
            dialog.Object,
            snackbarService.Object,
            snackbar,
            fileList,
            sortFilter,
            fileInput,
            conversion,
            listManager,
            header);

        return new MainViewModelContext
        {
            ViewModel = viewModel,
            Snackbar = snackbar,
            SortFilter = sortFilter,
            FileInput = fileInput,
            Conversion = conversion,
            ListManager = listManager,
            Header = header
        };
    }
}

internal sealed class MainViewModelContext
{
    public required MainViewModel ViewModel { get; init; }
    public required SnackbarViewModel Snackbar { get; init; }
    public required SortFilterViewModel SortFilter { get; init; }
    public required FileInputViewModel FileInput { get; init; }
    public required ConversionViewModel Conversion { get; init; }
    public required ListManagerViewModel ListManager { get; init; }
    public required HeaderViewModel Header { get; init; }
}

internal sealed class AppStatusChangedRecorder : IDisposable
{
    public List<AppStatus> Statuses { get; } = [];

    public AppStatusChangedRecorder()
    {
        WeakReferenceMessenger.Default.Register<AppStatusChangedMessage>(
            this,
            static (recipient, message) => ((AppStatusChangedRecorder)recipient).Statuses.Add(message.Value));
    }

    public void Dispose()
    {
        WeakReferenceMessenger.Default.UnregisterAll(this);
    }
}
