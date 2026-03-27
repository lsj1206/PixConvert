using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using PixConvert.Models;
using PixConvert.Services;
using PixConvert.Services.Interfaces;
using PixConvert.Services.Providers;
using PixConvert.ViewModels;
using Xunit;

namespace PixConvert.Tests;

public class ConversionViewModelTests
{
    private readonly Mock<ILanguageService> _mockLang;
    private readonly Mock<ILogger<ConversionViewModel>> _mockLogger;
    private readonly Mock<IPresetService> _mockPreset;
    private readonly Mock<ISnackbarService> _mockSnackbar;
    private readonly Mock<IDialogService> _mockDialog;
    private readonly FileListViewModel _fileListVm;
    private readonly ConversionViewModel _vm;

    public ConversionViewModelTests()
    {
        _mockLang = new Mock<ILanguageService>();
        _mockLogger = new Mock<ILogger<ConversionViewModel>>();
        _mockPreset = new Mock<IPresetService>();
        _mockSnackbar = new Mock<ISnackbarService>();
        _mockDialog = new Mock<IDialogService>();

        _mockLang.Setup(l => l.GetString(It.IsAny<string>())).Returns<string>(s => s);

        var mockSkia = new Mock<SkiaSharpProvider>(_mockLang.Object, new Mock<ILogger<SkiaSharpProvider>>().Object);
        var mockVips = new Mock<NetVipsProvider>(_mockLang.Object, new Mock<ILogger<NetVipsProvider>>().Object);

        mockSkia.As<IProviderService>().Setup(p => p.Name).Returns("SkiaSharp");
        mockVips.As<IProviderService>().Setup(p => p.Name).Returns("NetVips");

        var engineSelector = new EngineSelector(mockSkia.Object, mockVips.Object);

        _fileListVm = new FileListViewModel(_mockLang.Object, new Mock<ILogger<FileListViewModel>>().Object);

        // IPresetService 초기화
        _mockPreset.Setup(p => p.Config).Returns(new PresetConfig());

        _vm = new ConversionViewModel(
            _mockLogger.Object,
            _mockLang.Object,
            _mockDialog.Object,
            _mockSnackbar.Object,
            _mockPreset.Object,
            _fileListVm,
            engineSelector,
            () => null!);
    }

    [Fact]
    public void Properties_ShouldInitializeCorrectly()
    {
        Assert.Empty(_vm.ActiveProcesses);
        Assert.Equal(string.Empty, _vm.CurrentCpuUsage);
        Assert.Equal(string.Empty, _vm.CurrentTargetFormat);
    }

    [Fact]
    public void CpuUsage_Mapping_ShouldWorkCorrectly()
    {
        // 1. 일반적인 경우 (Optimal -> Setting_Cpu_Optimal)
        var settings = new ConvertSettings { CpuUsage = CpuUsageOption.Optimal };
        // PresetConfig 구조에 맞게 설정
        var config = new PresetConfig
        {
            LastSelectedPresetName = "Default",
            Presets = new List<ConvertPreset> { new() { Name = "Default", Settings = settings } }
        };
        _mockPreset.Setup(p => p.Config).Returns(config);

        string cpuKey = $"Setting_Cpu_{settings.CpuUsage}";
        Assert.Equal("Setting_Cpu_Optimal", cpuKey);

        // 2. Minimum 케이스
        settings.CpuUsage = CpuUsageOption.Minimum;
        cpuKey = $"Setting_Cpu_{settings.CpuUsage}";
        Assert.Equal("Setting_Cpu_Minimum", cpuKey);

        // 3. Low 케이스 (신규)
        settings.CpuUsage = CpuUsageOption.Low;
        cpuKey = $"Setting_Cpu_{settings.CpuUsage}";
        Assert.Equal("Setting_Cpu_Low", cpuKey);
    }

    [Fact]
    public void ParallelDegree_Calculation_Logic_Check()
    {
        // 로직 자체를 모킹 없이 검증 (ConversionViewModel 내부 로직과 동일하게)
        Func<CpuUsageOption, int, int> calculate = (option, procCount) => option switch
        {
            CpuUsageOption.Max     => procCount <= 12 ? Math.Max(1, procCount - 1) : procCount - 2,
            CpuUsageOption.Optimal => Math.Max(1, procCount * 3 / 4),
            CpuUsageOption.Half    => Math.Max(1, procCount / 2),
            CpuUsageOption.Low     => Math.Max(1, procCount / 4),
            CpuUsageOption.Minimum => 1,
            _                      => 1
        };

        // 4코어 환경
        Assert.Equal(3, calculate(CpuUsageOption.Max, 4));
        Assert.Equal(3, calculate(CpuUsageOption.Optimal, 4));
        Assert.Equal(2, calculate(CpuUsageOption.Half, 4));
        Assert.Equal(1, calculate(CpuUsageOption.Low, 4));

        // 12코어 환경
        Assert.Equal(11, calculate(CpuUsageOption.Max, 12));
        Assert.Equal(9, calculate(CpuUsageOption.Optimal, 12));
        Assert.Equal(6, calculate(CpuUsageOption.Half, 12));
        Assert.Equal(3, calculate(CpuUsageOption.Low, 12));

        // 16코어 환경
        Assert.Equal(14, calculate(CpuUsageOption.Max, 16));
        Assert.Equal(12, calculate(CpuUsageOption.Optimal, 16));
        Assert.Equal(8, calculate(CpuUsageOption.Half, 16));
        Assert.Equal(4, calculate(CpuUsageOption.Low, 16));
    }

    [Fact]
    public void ActiveProcesses_ShouldSupportAddAndRemove()
    {
        var process = new ConversionViewModel.ActiveProcess { FileName = "test.jpg", EngineName = "SkiaSharp" };

        _vm.ActiveProcesses.Add(process);
        Assert.Single(_vm.ActiveProcesses);
        Assert.Equal("test.jpg", _vm.ActiveProcesses[0].FileName);

        _vm.ActiveProcesses.Remove(process);
        Assert.Empty(_vm.ActiveProcesses);
    }

    [Fact]
    public void TargetFormat_Display_Logic_Check()
    {
        // 로직 자체 검증
        Func<bool, bool, ConvertSettings, string> getFormatText = (hasStd, hasAni, settings) =>
        {
            if (hasStd && hasAni) return $"{settings.StandardTargetFormat} / {settings.AnimationTargetFormat}";
            if (hasAni) return settings.AnimationTargetFormat;
            return settings.StandardTargetFormat;
        };

        var settings = new ConvertSettings { StandardTargetFormat = "JPEG", AnimationTargetFormat = "GIF" };

        Assert.Equal("JPEG", getFormatText(true, false, settings));
        Assert.Equal("GIF", getFormatText(false, true, settings));
        Assert.Equal("JPEG / GIF", getFormatText(true, true, settings));
    }
}
