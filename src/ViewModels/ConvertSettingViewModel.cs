using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PixConvert.Models;
using PixConvert.Services;
using PixConvert.Services.Interfaces;

namespace PixConvert.ViewModels;

/// <summary>
/// 변환 설정 및 프리셋 관리 기능(설계도 반영)을 담당하는 뷰모델입니다.
/// </summary>
public partial class ConvertSettingViewModel : ViewModelBase
{
    private readonly IPresetService _presetService;

    // --- 프리셋 관리 속성 ---

    /// <summary>사용 가능한 프리셋 목록 (드롭다운 바인딩용)</summary>
    public ObservableCollection<ConvertPreset> Presets => new(_presetService.Config.Presets);

    /// <summary>현재 선택된 프리셋</summary>
    [ObservableProperty]
    private ConvertPreset? _selectedPreset;

    /// <summary>프리셋 이름 편집용 (FilterTextBox 바인딩)</summary>
    [ObservableProperty]
    private string _presetNameEdit = string.Empty;

    // --- 변환 상세 옵션 (선택된 프리셋의 Settings와 동기화) ---

    [ObservableProperty] private string _standardTargetFormat = "JPEG";
    [ObservableProperty] private string _animationTargetFormat = "GIF";
    [ObservableProperty] private int _quality = 85;
    [ObservableProperty] private BackgroundColorOption _bgColorOption = BackgroundColorOption.White;
    [ObservableProperty] private string _customBackgroundColor = "#FFFFFF";
    [ObservableProperty] private bool _keepExif = false;
    [ObservableProperty] private OverwritePolicy _overwriteSide = OverwritePolicy.Suffix;
    [ObservableProperty] private OutputPathType _outputType = OutputPathType.SubFolder;
    [ObservableProperty] private string _customOutputPath = string.Empty;
    [ObservableProperty] private CpuUsageOption _cpuUsage = CpuUsageOption.Optimal;

    /// <summary>이미지 소스 포맷 태그 (선택 상태 관리용)</summary>
    public ObservableCollection<FormatTagViewModel> StandardSourceTags { get; } = new();
    public ObservableCollection<FormatTagViewModel> AnimationSourceTags { get; } = new();

    // --- 지원 목록 ---
    public string[] SupportedStandardTargets { get; } = ["JPEG", "PNG", "BMP", "WEBP", "AVIF"];
    public string[] SupportedAnimationTargets { get; } = ["GIF", "WEBP"];

    // Enum 목록 바인딩용
    public BackgroundColorOption[] BgColorOptions { get; } = (BackgroundColorOption[])System.Enum.GetValues(typeof(BackgroundColorOption));
    public OverwritePolicy[] OverwritePolicies { get; } = (OverwritePolicy[])System.Enum.GetValues(typeof(OverwritePolicy));
    public OutputPathType[] OutputPathTypes { get; } = (OutputPathType[])System.Enum.GetValues(typeof(OutputPathType));
    public CpuUsageOption[] CpuUsageOptions { get; } = (CpuUsageOption[])System.Enum.GetValues(typeof(CpuUsageOption));

    // --- 명령 ---
    public IRelayCommand CreatePresetCommand { get; }
    public IRelayCommand CopyPresetCommand { get; }
    public IRelayCommand RemovePresetCommand { get; }
    public IRelayCommand RenamePresetCommand { get; }
    public IRelayCommand ChangeOutputPathCommand { get; }

    public ConvertSettingViewModel(
        ILanguageService languageService,
        ILogger<ConvertSettingViewModel> logger,
        IPresetService presetService)
        : base(languageService, logger)
    {
        _presetService = presetService;

        // 태그 초기화
        InitializeTags();

        // 명령 정의
        CreatePresetCommand = new RelayCommand(CreatePreset);
        CopyPresetCommand = new RelayCommand(CopyPreset, () => SelectedPreset != null);
        RemovePresetCommand = new RelayCommand(RemovePreset, () => SelectedPreset != null && Presets.Count > 1);
        RenamePresetCommand = new RelayCommand(RenamePreset, () => SelectedPreset != null && !string.IsNullOrWhiteSpace(PresetNameEdit));
        ChangeOutputPathCommand = new RelayCommand(ChangeOutputPath);

        // 초기 프리셋 설정
        var lastPreset = Presets.FirstOrDefault(p => p.Name == _presetService.Config.LastSelectedPresetName) ?? Presets.FirstOrDefault();
        SelectedPreset = lastPreset;
    }

    private void InitializeTags()
    {
        string[] standard = ["jpeg", "png", "bmp", "webp", "avif"];
        string[] animation = ["gif", "webp"]; // webp-ani

        foreach (var f in standard) StandardSourceTags.Add(new FormatTagViewModel(f));
        foreach (var f in animation) AnimationSourceTags.Add(new FormatTagViewModel(f));
    }

    partial void OnSelectedPresetChanged(ConvertPreset? value)
    {
        if (value == null) return;

        _presetService.Config.LastSelectedPresetName = value.Name;
        PresetNameEdit = value.Name;
        LoadFromSettings(value.Settings);

        // 상태 갱신
        CopyPresetCommand.NotifyCanExecuteChanged();
        RemovePresetCommand.NotifyCanExecuteChanged();
        RenamePresetCommand.NotifyCanExecuteChanged();
    }

    private void LoadFromSettings(ConvertSettings s)
    {
        StandardTargetFormat = s.StandardTargetFormat ?? "JPEG";
        AnimationTargetFormat = s.AnimationTargetFormat ?? "GIF";
        Quality = s.Quality;
        BgColorOption = s.BgColorOption;
        CustomBackgroundColor = s.CustomBackgroundColor ?? "#FFFFFF";
        KeepExif = s.KeepExif;
        OverwriteSide = s.OverwriteSide;
        OutputType = s.OutputType;
        CustomOutputPath = s.CustomOutputPath ?? string.Empty;
        CpuUsage = s.CpuUsage;

        // 태그 업데이트
        var stdFormats = s.StandardSourceFormats ?? ["jpeg", "png", "bmp", "webp", "avif"];
        var aniFormats = s.AnimationSourceFormats ?? ["gif", "webp"];

        foreach (var tag in StandardSourceTags) tag.IsSelected = stdFormats.Contains(tag.Format);
        foreach (var tag in AnimationSourceTags) tag.IsSelected = aniFormats.Contains(tag.Format);
    }

    /// <summary>UI의 변경사항을 현재 선택된 프리셋의 Settings 객체에 반영합니다.</summary>
    public void SyncToSettings()
    {
        if (SelectedPreset == null) return;
        var s = SelectedPreset.Settings;

        s.StandardTargetFormat = StandardTargetFormat;
        s.AnimationTargetFormat = AnimationTargetFormat;
        s.Quality = Quality;
        s.BgColorOption = BgColorOption;
        s.CustomBackgroundColor = CustomBackgroundColor;
        s.KeepExif = KeepExif;
        s.OverwriteSide = OverwriteSide;
        s.OutputType = OutputType;
        s.CustomOutputPath = CustomOutputPath;
        s.CpuUsage = CpuUsage;

        s.StandardSourceFormats = StandardSourceTags.Where(t => t.IsSelected).Select(t => t.Format).ToList();
        s.AnimationSourceFormats = AnimationSourceTags.Where(t => t.IsSelected).Select(t => t.Format).ToList();
    }

    private void CreatePreset()
    {
        string newName = $"Preset_{Presets.Count + 1}";
        _presetService.AddPreset(newName, new ConvertSettings());
        OnPropertyChanged(nameof(Presets));
        SelectedPreset = Presets.LastOrDefault();
    }

    private void CopyPreset()
    {
        if (SelectedPreset == null) return;
        string newName = $"{SelectedPreset.Name}_Copy";
        _presetService.CopyPreset(SelectedPreset.Name, newName);
        OnPropertyChanged(nameof(Presets));
        SelectedPreset = Presets.FirstOrDefault(p => p.Name == newName);
    }

    private void RemovePreset()
    {
        if (SelectedPreset == null) return;
        _presetService.RemovePreset(SelectedPreset.Name);
        OnPropertyChanged(nameof(Presets));
        SelectedPreset = Presets.FirstOrDefault();
    }

    private void RenamePreset()
    {
        if (SelectedPreset == null || string.IsNullOrWhiteSpace(PresetNameEdit)) return;
        string oldName = SelectedPreset.Name;
        _presetService.RenamePreset(oldName, PresetNameEdit);
        OnPropertyChanged(nameof(Presets));
        SelectedPreset = Presets.FirstOrDefault(p => p.Name == PresetNameEdit);
    }

    private void ChangeOutputPath()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "출력 폴더 선택" // TODO: 다국어 리소스 적용 권장
        };

        if (dialog.ShowDialog() == true)
        {
            CustomOutputPath = dialog.FolderName;
        }
    }
}

/// <summary>포맷 태그의 선택 상태를 관리하기 위한 미니 뷰모델</summary>
public partial class FormatTagViewModel : ObservableObject
{
    public string Format { get; }
    [ObservableProperty] private bool _isSelected;
    public FormatTagViewModel(string format) => Format = format;
}
