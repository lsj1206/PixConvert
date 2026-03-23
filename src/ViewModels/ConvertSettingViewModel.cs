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
    [ObservableProperty] private OutputLocationType _outputLocation = OutputLocationType.SameAsOriginal;
    [ObservableProperty] private OutputFolderStrategy _folderStrategy = OutputFolderStrategy.CreateFolder;
    [ObservableProperty] private string _outputSubFolderName = "PixConvert";
    [ObservableProperty] private string _customOutputPath = string.Empty;
    [ObservableProperty] private CpuUsageOption _cpuUsage = CpuUsageOption.Optimal;

    partial void OnStandardTargetFormatChanged(string value) => OnPropertyChanged(nameof(IsLossyFormat));
    partial void OnAnimationTargetFormatChanged(string value) => OnPropertyChanged(nameof(IsLossyFormat));

    /// <summary>
    /// 현재 선택된 포맷 중 하나라도 손실 압축(Quality 적용) 포맷인지 여부를 반환합니다.
    /// UI의 Quality 슬라이더 활성화 여부를 결정합니다.
    /// </summary>
    public bool IsLossyFormat
    {
        get
        {
            string std = StandardTargetFormat?.ToUpperInvariant() ?? "";
            string ani = AnimationTargetFormat?.ToUpperInvariant() ?? "";
            bool stdLossy = std is "JPEG" or "WEBP" or "AVIF";
            bool aniLossy = ani is "WEBP" or "AVIF";
            return stdLossy || aniLossy;
        }
    }

    /// <summary>목표 포맷 태그 (단일 선택 UI용)</summary>
    public ObservableCollection<FormatTagViewModel> StandardTargetTags { get; } = new();
    public ObservableCollection<FormatTagViewModel> AnimationTargetTags { get; } = new();

    // Enum 목록 바인딩용
    public BackgroundColorOption[] BgColorOptions { get; } = (BackgroundColorOption[])System.Enum.GetValues(typeof(BackgroundColorOption));
    public OverwritePolicy[] OverwritePolicies { get; } = (OverwritePolicy[])System.Enum.GetValues(typeof(OverwritePolicy));
    public OutputLocationType[] OutputLocationTypes { get; } = (OutputLocationType[])System.Enum.GetValues(typeof(OutputLocationType));
    public OutputFolderStrategy[] FolderStrategies { get; } = (OutputFolderStrategy[])System.Enum.GetValues(typeof(OutputFolderStrategy));
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

    /// <summary>
    /// 변환 대상 목표 포맷들의 초기 리스트를 구성합니다.
    /// </summary>
    private void InitializeTags()
    {
        string[] standardTargets = ["JPEG", "PNG", "BMP", "WEBP", "AVIF"];
        string[] animationTargets = ["GIF", "WEBP", "AVIF"];

        foreach (var f in standardTargets)
        {
            var tag = new FormatTagViewModel(f);
            tag.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(FormatTagViewModel.IsSelected) && tag.IsSelected)
                {
                    // 하나가 선택되면 나머지는 선택 해제
                    foreach (var other in StandardTargetTags.Where(t => t != tag))
                        other.IsSelected = false;

                    StandardTargetFormat = tag.Format;
                }
            };
            StandardTargetTags.Add(tag);
        }

        foreach (var f in animationTargets)
        {
            var tag = new FormatTagViewModel(f);
            tag.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(FormatTagViewModel.IsSelected) && tag.IsSelected)
                {
                    // 하나가 선택되면 나머지는 선택 해제
                    foreach (var other in AnimationTargetTags.Where(t => t != tag))
                        other.IsSelected = false;

                    AnimationTargetFormat = tag.Format;
                }
            };
            AnimationTargetTags.Add(tag);
        }
    }

    /// <summary>
    /// 선택된 프리셋이 변경될 때 자동으로 호출되는 콜백(ObservableProperty 기능)입니다.
    /// 바뀐 프리셋의 설정값들을 뷰모델(UI) 상태에 로드합니다.
    /// </summary>
    /// <param name="value">새롭게 선택된 프리셋 객체</param>
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

    /// <summary>
    /// 인자로 받은 프리셋 설정 모델(ConvertSettings)의 값을 읽어와 현재 뷰모델의 바인딩 속성들에 채워넣습니다.
    /// </summary>
    private void LoadFromSettings(ConvertSettings s)
    {
        StandardTargetFormat = s.StandardTargetFormat ?? "JPEG";
        AnimationTargetFormat = s.AnimationTargetFormat ?? "GIF";
        Quality = s.Quality;
        BgColorOption = s.BgColorOption;
        CustomBackgroundColor = s.CustomBackgroundColor ?? "#FFFFFF";
        KeepExif = s.KeepExif;
        OverwriteSide = s.OverwriteSide;
        OutputLocation = s.OutputLocation;
        FolderStrategy = s.FolderStrategy;
        OutputSubFolderName = s.OutputSubFolderName ?? "PixConvert";
        CustomOutputPath = s.CustomOutputPath ?? string.Empty;
        CpuUsage = s.CpuUsage;

        // 태그 선택 상태 동기화
        foreach (var tag in StandardTargetTags) tag.IsSelected = (tag.Format == StandardTargetFormat);
        foreach (var tag in AnimationTargetTags) tag.IsSelected = (tag.Format == AnimationTargetFormat);
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
        s.OutputLocation = OutputLocation;
        s.FolderStrategy = FolderStrategy;
        s.OutputSubFolderName = OutputSubFolderName;
        s.CustomOutputPath = CustomOutputPath;
        s.CpuUsage = CpuUsage;
    }

    /// <summary>
    /// 빈 설정을 가진 새로운 프리셋을 생성하여 리스트 마지막에 추가합니다.
    /// </summary>
    private void CreatePreset()
    {
        string newName = $"Preset_{Presets.Count + 1}";
        _presetService.AddPreset(newName, new ConvertSettings());
        OnPropertyChanged(nameof(Presets));
        SelectedPreset = Presets.LastOrDefault();
    }

    /// <summary>
    /// 현재 선택된 프리셋의 설정을 그대로 복제하여 새로운 이름으로 추가합니다.
    /// </summary>
    private void CopyPreset()
    {
        if (SelectedPreset == null) return;
        string newName = $"{SelectedPreset.Name}_Copy";
        _presetService.CopyPreset(SelectedPreset.Name, newName);
        OnPropertyChanged(nameof(Presets));
        SelectedPreset = Presets.FirstOrDefault(p => p.Name == newName);
    }

    /// <summary>
    /// 현재 선택된 프리셋을 삭제합니다. 최소 1개는 남도록 CanExecute에서 제어합니다.
    /// </summary>
    private void RemovePreset()
    {
        if (SelectedPreset == null) return;
        _presetService.RemovePreset(SelectedPreset.Name);
        OnPropertyChanged(nameof(Presets));
        SelectedPreset = Presets.FirstOrDefault();
    }

    /// <summary>
    /// 현재 선택된 프리셋의 이름을 텍스트박스(PresetNameEdit)에 입력된 새 이름으로 변경합니다.
    /// </summary>
    private void RenamePreset()
    {
        if (SelectedPreset == null || string.IsNullOrWhiteSpace(PresetNameEdit)) return;
        string oldName = SelectedPreset.Name;
        _presetService.RenamePreset(oldName, PresetNameEdit);
        OnPropertyChanged(nameof(Presets));
        SelectedPreset = Presets.FirstOrDefault(p => p.Name == PresetNameEdit);
    }

    /// <summary>
    /// 변환 결과물이 저장될 커스텀 출력 폴더 경로를 윈도우 다이얼로그를 통해 지정합니다.
    /// </summary>
    private void ChangeOutputPath()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = _languageService.GetString("Dlg_Title_SelectOutputPath")
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
