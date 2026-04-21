using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PixConvert.Models;
using PixConvert.Services;
using PixConvert.Services.Interfaces;

namespace PixConvert.ViewModels;

public enum BackgroundColorOption { White, Black, Custom }

public partial class ConvertSettingViewModel : ViewModelBase
{
    // 포맷/무손실 변경 시 함께 다시 계산해야 하는 UI 표시 속성들을 그룹으로 관리한다.
    private static readonly string[] StandardTargetFormats = ["JPEG", "PNG", "BMP", "WEBP", "AVIF"];
    private static readonly string[] AnimationTargetFormats = ["GIF", "WEBP"];
    private static readonly string[] StandardTargetDependentProperties =
    [
        nameof(StandardShowLossless),
        nameof(StandardShowQuality),
        nameof(StandardShowBackgroundColor),
        nameof(StandardShowJpegChromaSubsampling),
        nameof(StandardShowPngCompression),
        nameof(StandardShowAvifChromaSubsampling),
        nameof(StandardCanEditAvifChromaSubsampling),
        nameof(StandardShowAvifEncodingEffort),
        nameof(StandardShowAvifBitDepth)
    ];
    private static readonly string[] AnimationTargetDependentProperties =
    [
        nameof(ShowAnimationOptionsSection),
        nameof(AnimationShowLossless),
        nameof(AnimationShowQuality),
        nameof(AnimationShowGifPalettePreset),
        nameof(AnimationShowGifEncodingEffort),
        nameof(AnimationShowGifInterframeMaxError),
        nameof(AnimationShowGifInterpaletteMaxError),
        nameof(AnimationShowWebpEncodingEffort),
        nameof(AnimationShowWebpPreset),
        nameof(AnimationShowWebpPreserveTransparentPixels)
    ];
    private static readonly string[] StandardLosslessDependentProperties =
    [
        nameof(StandardShowAvifChromaSubsampling),
        nameof(StandardShowQuality),
        nameof(StandardCanEditAvifChromaSubsampling)
    ];
    private static readonly string[] AnimationLosslessDependentProperties =
    [
        nameof(AnimationShowQuality),
        nameof(AnimationShowWebpPreset),
        nameof(AnimationShowWebpPreserveTransparentPixels)
    ];

    private readonly IPresetService _presetService;
    private readonly IPathPickerService _pathPickerService;
    private bool _isSyncingTargetTags;

    public ObservableCollection<ConvertPreset> Presets => new(_presetService.Config.Presets);

    [ObservableProperty] private ConvertPreset? _selectedPreset;
    [ObservableProperty] private string _presetNameEdit = string.Empty;
    [ObservableProperty] private string _standardTargetFormat = "JPEG";
    [ObservableProperty] private string? _animationTargetFormat = "GIF";
    [ObservableProperty] private int _standardQuality = 85;
    [ObservableProperty] private bool _standardLossless;
    [ObservableProperty] private JpegChromaSubsamplingMode _standardJpegChromaSubsampling = JpegChromaSubsamplingMode.Chroma444;
    [ObservableProperty] private int _standardPngCompressionLevel = 6;
    [ObservableProperty] private AvifChromaSubsamplingMode _standardAvifChromaSubsampling = AvifChromaSubsamplingMode.Auto;
    [ObservableProperty] private int _standardAvifEncodingEffort = 4;
    [ObservableProperty] private AvifBitDepthMode _standardAvifBitDepth = AvifBitDepthMode.Auto;
    [ObservableProperty] private BackgroundColorOption _standardBgColorOption = BackgroundColorOption.White;
    [ObservableProperty] private string _standardCustomBackgroundColor = "#FFFFFF";
    [ObservableProperty] private int _animationQuality = 85;
    [ObservableProperty] private bool _animationLossless;
    [ObservableProperty] private int _animationWebpEncodingEffort = 4;
    [ObservableProperty] private WebpPresetMode _animationWebpPreset = WebpPresetMode.Default;
    [ObservableProperty] private bool _animationWebpPreserveTransparentPixels;
    [ObservableProperty] private GifPalettePreset _animationGifPalettePreset = GifPalettePreset.Standard;
    [ObservableProperty] private int _animationGifEncodingEffort = 6;
    [ObservableProperty] private double _animationGifInterframeMaxError;
    [ObservableProperty] private double _animationGifInterpaletteMaxError;
    [ObservableProperty] private string _animationGifInterframeMaxErrorText = "0";
    [ObservableProperty] private string _animationGifInterpaletteMaxErrorText = "0";
    [ObservableProperty] private OverwritePolicy _overwritePolicy = OverwritePolicy.Suffix;
    [ObservableProperty] private SaveLocationType _saveLocation = SaveLocationType.SameAsOriginal;
    [ObservableProperty] private SaveFolderMethod _folderMethod = SaveFolderMethod.CreateFolder;
    [ObservableProperty] private string _outputSubFolderName = "PixConvert";
    [ObservableProperty] private string _customOutputPath = string.Empty;
    [ObservableProperty] private CpuUsageOption _cpuUsage = CpuUsageOption.Optimal;

    public ObservableCollection<FormatTagViewModel> StandardTargetTags { get; } = new();
    public ObservableCollection<FormatTagViewModel> AnimationTargetTags { get; } = new();

    public IRelayCommand CreatePresetCommand { get; }
    public IRelayCommand CopyPresetCommand { get; }
    public IRelayCommand RemovePresetCommand { get; }
    public IRelayCommand RenamePresetCommand { get; }
    public IRelayCommand ChangeOutputPathCommand { get; }

    public bool StandardShowLossless =>
        SettingOptionCatalog.Supports(SettingOptionSection.Standard, SettingOptionKey.Lossless, StandardTargetFormat);

    public bool StandardShowQuality =>
        SettingOptionCatalog.Supports(SettingOptionSection.Standard, SettingOptionKey.Quality, StandardTargetFormat) &&
        !(StandardShowLossless && StandardLossless);

    public bool StandardShowBackgroundColor =>
        SettingOptionCatalog.Supports(SettingOptionSection.Standard, SettingOptionKey.BackgroundColor, StandardTargetFormat);

    public bool StandardShowJpegChromaSubsampling =>
        SettingOptionCatalog.Supports(SettingOptionSection.Standard, SettingOptionKey.JpegChromaSubsampling, StandardTargetFormat);

    public bool StandardShowPngCompression =>
        SettingOptionCatalog.Supports(SettingOptionSection.Standard, SettingOptionKey.PngCompression, StandardTargetFormat);

    public bool StandardShowAvifChromaSubsampling =>
        SettingOptionCatalog.Supports(SettingOptionSection.Standard, SettingOptionKey.AvifChromaSubsampling, StandardTargetFormat) &&
        !StandardLossless;

    public bool StandardCanEditAvifChromaSubsampling =>
        StandardShowAvifChromaSubsampling && !StandardLossless;

    public bool StandardShowAvifEncodingEffort =>
        SettingOptionCatalog.Supports(SettingOptionSection.Standard, SettingOptionKey.AvifEncodingEffort, StandardTargetFormat);

    public bool StandardShowAvifBitDepth =>
        SettingOptionCatalog.Supports(SettingOptionSection.Standard, SettingOptionKey.AvifBitDepth, StandardTargetFormat);

    public bool ShowAnimationOptionsSection => !string.IsNullOrWhiteSpace(AnimationTargetFormat);

    public bool AnimationShowLossless =>
        ShowAnimationOptionsSection &&
        SettingOptionCatalog.Supports(SettingOptionSection.Animation, SettingOptionKey.Lossless, AnimationTargetFormat);

    public bool AnimationShowQuality =>
        ShowAnimationOptionsSection &&
        SettingOptionCatalog.Supports(SettingOptionSection.Animation, SettingOptionKey.Quality, AnimationTargetFormat) &&
        !(AnimationShowLossless && AnimationLossless);

    public bool AnimationShowGifPalettePreset =>
        ShowAnimationOptionsSection &&
        SettingOptionCatalog.Supports(SettingOptionSection.Animation, SettingOptionKey.GifPalettePreset, AnimationTargetFormat);

    public bool AnimationShowGifEncodingEffort =>
        ShowAnimationOptionsSection &&
        SettingOptionCatalog.Supports(SettingOptionSection.Animation, SettingOptionKey.GifEncodingEffort, AnimationTargetFormat);

    public bool AnimationShowGifInterframeMaxError =>
        ShowAnimationOptionsSection &&
        SettingOptionCatalog.Supports(SettingOptionSection.Animation, SettingOptionKey.GifInterframeMaxError, AnimationTargetFormat);

    public bool AnimationShowGifInterpaletteMaxError =>
        ShowAnimationOptionsSection &&
        SettingOptionCatalog.Supports(SettingOptionSection.Animation, SettingOptionKey.GifInterpaletteMaxError, AnimationTargetFormat);

    public bool AnimationShowWebpEncodingEffort =>
        ShowAnimationOptionsSection &&
        SettingOptionCatalog.Supports(SettingOptionSection.Animation, SettingOptionKey.WebpEncodingEffort, AnimationTargetFormat);

    public bool AnimationShowWebpPreset =>
        ShowAnimationOptionsSection &&
        !AnimationLossless &&
        SettingOptionCatalog.Supports(SettingOptionSection.Animation, SettingOptionKey.WebpPreset, AnimationTargetFormat);

    public bool AnimationShowWebpPreserveTransparentPixels =>
        ShowAnimationOptionsSection &&
        AnimationLossless &&
        SettingOptionCatalog.Supports(SettingOptionSection.Animation, SettingOptionKey.WebpPreserveTransparentPixels, AnimationTargetFormat);

    /// <summary>
    /// 프리셋 편집기를 생성하고 명령, 태그, 마지막 선택 프리셋을 초기화합니다.
    /// </summary>
    public ConvertSettingViewModel(
        ILanguageService languageService,
        ILogger<ConvertSettingViewModel> logger,
        IPresetService presetService,
        IPathPickerService pathPickerService)
        : base(languageService, logger)
    {
        _presetService = presetService;
        _pathPickerService = pathPickerService;

        InitializeTags();

        CreatePresetCommand = new RelayCommand(CreatePreset);
        CopyPresetCommand = new RelayCommand(CopyPreset, () => SelectedPreset != null);
        RemovePresetCommand = new RelayCommand(RemovePreset, () => SelectedPreset != null && Presets.Count > 1);
        RenamePresetCommand = new RelayCommand(RenamePreset, () => SelectedPreset != null && !string.IsNullOrWhiteSpace(PresetNameEdit));
        ChangeOutputPathCommand = new RelayCommand(ChangeOutputPath);

        var lastPreset = Presets.FirstOrDefault(p => p.Name == _presetService.Config.LastSelectedPresetName) ?? Presets.FirstOrDefault();
        SelectedPreset = lastPreset;
    }

    /// <summary>
    /// 선택된 프리셋을 편집 상태에 반영하고 명령 실행 가능 상태를 갱신합니다.
    /// </summary>
    partial void OnSelectedPresetChanged(ConvertPreset? value)
    {
        if (value == null)
            return;

        _presetService.Config.LastSelectedPresetName = value.Name;
        PresetNameEdit = value.Name;
        LoadFromSettings(value.Settings);

        CopyPresetCommand.NotifyCanExecuteChanged();
        RemovePresetCommand.NotifyCanExecuteChanged();
        RenamePresetCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// 일반 이미지 품질 값을 허용 범위로 보정합니다.
    /// </summary>
    partial void OnStandardQualityChanged(int value)
    {
        int coerced = Math.Clamp(value, 1, 100);
        if (value != coerced)
            StandardQuality = coerced;
    }

    /// <summary>
    /// 애니메이션 품질 값을 허용 범위로 보정합니다.
    /// </summary>
    partial void OnAnimationQualityChanged(int value)
    {
        int coerced = Math.Clamp(value, 1, 100);
        if (value != coerced)
            AnimationQuality = coerced;
    }

    /// <summary>
    /// PNG 압축 레벨을 허용 범위로 보정합니다.
    /// </summary>
    partial void OnStandardPngCompressionLevelChanged(int value)
    {
        int coerced = Math.Clamp(value, 0, 9);
        if (value != coerced)
            StandardPngCompressionLevel = coerced;
    }

    /// <summary>
    /// AVIF 인코딩 노력 값을 허용 범위로 보정합니다.
    /// </summary>
    partial void OnStandardAvifEncodingEffortChanged(int value)
    {
        int coerced = Math.Clamp(value, 0, 9);
        if (value != coerced)
            StandardAvifEncodingEffort = coerced;
    }

    /// <summary>
    /// 애니메이션 WebP 인코딩 노력 값을 허용 범위로 보정합니다.
    /// </summary>
    partial void OnAnimationWebpEncodingEffortChanged(int value)
    {
        int coerced = Math.Clamp(value, 0, 6);
        if (value != coerced)
            AnimationWebpEncodingEffort = coerced;
    }

    /// <summary>
    /// GIF 인코딩 노력 값을 허용 범위로 보정합니다.
    /// </summary>
    partial void OnAnimationGifEncodingEffortChanged(int value)
    {
        int coerced = Math.Clamp(value, 0, 9);
        if (value != coerced)
            AnimationGifEncodingEffort = coerced;
    }

    /// <summary>
    /// GIF 프레임 간 오차 값을 정규화하고 바인딩된 텍스트와 동기화합니다.
    /// </summary>
    partial void OnAnimationGifInterframeMaxErrorChanged(double value)
    {
        double coerced = CoerceGifErrorValue(value);
        if (Math.Abs(value - coerced) > double.Epsilon)
        {
            AnimationGifInterframeMaxError = coerced;
            return;
        }

        string formatted = FormatGifErrorValue(coerced);
        if (!AnimationGifInterframeMaxErrorText.Equals(formatted, StringComparison.Ordinal))
            AnimationGifInterframeMaxErrorText = formatted;
    }

    /// <summary>
    /// GIF 팔레트 간 오차 값을 정규화하고 바인딩된 텍스트와 동기화합니다.
    /// </summary>
    partial void OnAnimationGifInterpaletteMaxErrorChanged(double value)
    {
        double coerced = CoerceGifErrorValue(value);
        if (Math.Abs(value - coerced) > double.Epsilon)
        {
            AnimationGifInterpaletteMaxError = coerced;
            return;
        }

        string formatted = FormatGifErrorValue(coerced);
        if (!AnimationGifInterpaletteMaxErrorText.Equals(formatted, StringComparison.Ordinal))
            AnimationGifInterpaletteMaxErrorText = formatted;
    }

    /// <summary>
    /// 사용자가 입력한 프레임 간 GIF 오차 텍스트를 파싱해 유효할 때 반영합니다.
    /// </summary>
    partial void OnAnimationGifInterframeMaxErrorTextChanged(string value)
    {
        ApplyGifErrorText(value, true);
    }

    /// <summary>
    /// 사용자가 입력한 팔레트 간 GIF 오차 텍스트를 파싱해 유효할 때 반영합니다.
    /// </summary>
    partial void OnAnimationGifInterpaletteMaxErrorTextChanged(string value)
    {
        ApplyGifErrorText(value, false);
    }

    /// <summary>
    /// 일반 대상 포맷이 바뀔 때 포맷 의존 UI 상태를 다시 계산합니다.
    /// </summary>
    partial void OnStandardTargetFormatChanged(string value) => RefreshTargetFormatDependentProperties();

    /// <summary>
    /// 애니메이션 대상 포맷이 바뀔 때 포맷 의존 UI 상태를 다시 계산합니다.
    /// </summary>
    partial void OnAnimationTargetFormatChanged(string? value) => RefreshTargetFormatDependentProperties();

    /// <summary>
    /// 일반 출력 옵션에서 무손실 여부에 따라 달라지는 UI 상태를 갱신합니다.
    /// </summary>
    partial void OnStandardLosslessChanged(bool value)
    {
        NotifyPropertiesChanged(StandardLosslessDependentProperties);
    }

    /// <summary>
    /// 애니메이션 출력 옵션에서 무손실 여부에 따라 달라지는 UI 상태를 갱신합니다.
    /// </summary>
    partial void OnAnimationLosslessChanged(bool value)
    {
        NotifyPropertiesChanged(AnimationLosslessDependentProperties);
    }

    /// <summary>
    /// 일반 출력과 애니메이션 출력에 사용할 대상 포맷 태그를 생성합니다.
    /// </summary>
    private void InitializeTags()
    {
        InitializeTagCollection(StandardTargetTags, StandardTargetFormats, HandleStandardTargetTagSelectionChanged);
        InitializeTagCollection(AnimationTargetTags, AnimationTargetFormats, HandleAnimationTargetTagSelectionChanged);
    }

    /// <summary>
    /// 선택된 프리셋 설정을 일반, 애니메이션, 출력 편집 상태에 각각 불러옵니다.
    /// </summary>
    private void LoadFromSettings(ConvertSettings settings)
    {
        LoadStandardSettings(settings);
        LoadAnimationSettings(settings);
        LoadOutputSettings(settings);
        SyncTargetTags();
    }

    /// <summary>
    /// 현재 편집 상태를 선택된 프리셋 설정에 다시 기록합니다.
    /// </summary>
    public void SyncToSettings()
    {
        if (SelectedPreset == null)
            return;

        var settings = SelectedPreset.Settings;
        SaveStandardSettings(settings);
        SaveAnimationSettings(settings);
        SaveOutputSettings(settings);
    }

    /// <summary>
    /// 새 프리셋을 만들고 프리셋 목록에서 선택합니다.
    /// </summary>
    private void CreatePreset()
    {
        string newName = $"Preset_{Presets.Count + 1}";
        _presetService.AddPreset(newName, new ConvertSettings());
        RefreshPresetCollection(newName);
    }

    /// <summary>
    /// 선택된 프리셋을 복사하고 복사본을 선택합니다.
    /// </summary>
    private void CopyPreset()
    {
        if (SelectedPreset == null)
            return;

        string newName = $"{SelectedPreset.Name}_Copy";
        _presetService.CopyPreset(SelectedPreset.Name, newName);
        RefreshPresetCollection(newName);
    }

    /// <summary>
    /// 선택된 프리셋을 삭제하고 다음 사용 가능한 프리셋으로 이동합니다.
    /// </summary>
    private void RemovePreset()
    {
        if (SelectedPreset == null)
            return;

        _presetService.RemovePreset(SelectedPreset.Name);
        RefreshPresetCollection();
    }

    /// <summary>
    /// 선택된 프리셋 이름을 변경하고 변경된 프리셋을 계속 선택 상태로 유지합니다.
    /// </summary>
    private void RenamePreset()
    {
        if (SelectedPreset == null || string.IsNullOrWhiteSpace(PresetNameEdit))
            return;

        string oldName = SelectedPreset.Name;
        _presetService.RenamePreset(oldName, PresetNameEdit);
        RefreshPresetCollection(PresetNameEdit);
    }

    /// <summary>
    /// 폴더 선택기를 열고 선택된 사용자 지정 출력 경로를 저장합니다.
    /// </summary>
    private void ChangeOutputPath()
    {
        string? path = _pathPickerService.PickFolder(_languageService.GetString("Dlg_Title_SelectOutputPath"));
        if (!string.IsNullOrWhiteSpace(path))
            CustomOutputPath = path;
    }

    /// <summary>
    /// 대상 포맷 변경에 따라 태그 선택 상태와 관련 UI 속성을 다시 계산합니다.
    /// </summary>
    private void RefreshTargetFormatDependentProperties()
    {
        SyncTargetTags();
        NotifyPropertiesChanged(StandardTargetDependentProperties);
        NotifyPropertiesChanged(AnimationTargetDependentProperties);
    }

    /// <summary>
    /// 일반 대상 포맷 태그 변경을 일반 포맷 속성에 반영합니다.
    /// </summary>
    private void HandleStandardTargetTagSelectionChanged(FormatTagViewModel tag)
    {
        HandleTargetTagSelectionChanged(
            StandardTargetTags,
            tag,
            selectedTag => StandardTargetFormat = selectedTag.Format,
            () => RestoreRequiredSelection(tag));
    }

    /// <summary>
    /// 애니메이션 대상 포맷 태그 변경을 애니메이션 포맷 속성에 반영합니다.
    /// </summary>
    private void HandleAnimationTargetTagSelectionChanged(FormatTagViewModel tag)
    {
        HandleTargetTagSelectionChanged(
            AnimationTargetTags,
            tag,
            selectedTag => AnimationTargetFormat = selectedTag.Format,
            () => AnimationTargetFormat = null);
    }

    /// <summary>
    /// 현재 대상 포맷 속성과 태그 선택 상태를 동기화합니다.
    /// </summary>
    private void SyncTargetTags()
    {
        if (_isSyncingTargetTags)
            return;

        WithTargetTagSync(() =>
        {
            SyncTagSelection(StandardTargetTags, StandardTargetFormat);
            SyncTagSelection(AnimationTargetTags, AnimationTargetFormat);
        });
    }

    /// <summary>
    /// 프리셋의 일반 포맷 설정을 편집 필드에 불러옵니다.
    /// </summary>
    private void LoadStandardSettings(ConvertSettings settings)
    {
        StandardTargetFormat = settings.StandardTargetFormat;
        StandardQuality = settings.StandardQuality;
        StandardLossless = settings.StandardLossless;
        StandardJpegChromaSubsampling = settings.StandardJpegChromaSubsampling;
        StandardPngCompressionLevel = settings.StandardPngCompressionLevel;
        StandardAvifChromaSubsampling = settings.StandardAvifChromaSubsampling;
        StandardAvifEncodingEffort = settings.StandardAvifEncodingEffort;
        StandardAvifBitDepth = settings.StandardAvifBitDepth;
        StandardCustomBackgroundColor = settings.StandardBackgroundColor;
        StandardBgColorOption = ParseBackgroundColorOption(StandardCustomBackgroundColor);
    }

    /// <summary>
    /// 프리셋의 애니메이션 포맷 설정을 편집 필드에 불러옵니다.
    /// </summary>
    private void LoadAnimationSettings(ConvertSettings settings)
    {
        AnimationTargetFormat = settings.AnimationTargetFormat;
        AnimationQuality = settings.AnimationQuality;
        AnimationLossless = settings.AnimationLossless;
        AnimationWebpEncodingEffort = settings.AnimationWebpEncodingEffort;
        AnimationWebpPreset = settings.AnimationWebpPreset;
        AnimationWebpPreserveTransparentPixels = settings.AnimationWebpPreserveTransparentPixels;
        AnimationGifPalettePreset = settings.AnimationGifPalettePreset;
        AnimationGifEncodingEffort = settings.AnimationGifEncodingEffort;
        AnimationGifInterframeMaxError = settings.AnimationGifInterframeMaxError;
        AnimationGifInterpaletteMaxError = settings.AnimationGifInterpaletteMaxError;
        AnimationGifInterframeMaxErrorText = FormatGifErrorValue(AnimationGifInterframeMaxError);
        AnimationGifInterpaletteMaxErrorText = FormatGifErrorValue(AnimationGifInterpaletteMaxError);
    }

    /// <summary>
    /// 프리셋의 출력 경로와 덮어쓰기 설정을 편집 필드에 불러옵니다.
    /// </summary>
    private void LoadOutputSettings(ConvertSettings settings)
    {
        OverwritePolicy = settings.OverwritePolicy;
        SaveLocation = settings.SaveLocation;
        FolderMethod = settings.FolderMethod;
        OutputSubFolderName = settings.OutputSubFolderName;
        CustomOutputPath = settings.CustomOutputPath;
        CpuUsage = settings.CpuUsage;
    }

    /// <summary>
    /// 현재 일반 포맷 편집 값을 프리셋 설정에 저장합니다.
    /// </summary>
    private void SaveStandardSettings(ConvertSettings settings)
    {
        settings.StandardTargetFormat = StandardTargetFormat;
        settings.StandardQuality = StandardQuality;
        settings.StandardLossless = StandardLossless;
        settings.StandardJpegChromaSubsampling = StandardJpegChromaSubsampling;
        settings.StandardPngCompressionLevel = StandardPngCompressionLevel;
        settings.StandardAvifChromaSubsampling = StandardAvifChromaSubsampling;
        settings.StandardAvifEncodingEffort = StandardAvifEncodingEffort;
        settings.StandardAvifBitDepth = StandardAvifBitDepth;
        settings.StandardBackgroundColor = ResolveBackgroundColor(StandardBgColorOption, StandardCustomBackgroundColor);
    }

    /// <summary>
    /// 현재 애니메이션 포맷 편집 값을 프리셋 설정에 저장합니다.
    /// </summary>
    private void SaveAnimationSettings(ConvertSettings settings)
    {
        settings.AnimationTargetFormat = AnimationTargetFormat;
        settings.AnimationQuality = AnimationQuality;
        settings.AnimationLossless = AnimationLossless;
        settings.AnimationWebpEncodingEffort = AnimationWebpEncodingEffort;
        settings.AnimationWebpPreset = AnimationWebpPreset;
        settings.AnimationWebpPreserveTransparentPixels = AnimationWebpPreserveTransparentPixels;
        settings.AnimationGifPalettePreset = AnimationGifPalettePreset;
        settings.AnimationGifEncodingEffort = AnimationGifEncodingEffort;
        settings.AnimationGifInterframeMaxError = AnimationGifInterframeMaxError;
        settings.AnimationGifInterpaletteMaxError = AnimationGifInterpaletteMaxError;
    }

    /// <summary>
    /// 현재 출력 경로와 덮어쓰기 편집 값을 프리셋 설정에 저장합니다.
    /// </summary>
    private void SaveOutputSettings(ConvertSettings settings)
    {
        settings.OverwritePolicy = OverwritePolicy;
        settings.SaveLocation = SaveLocation;
        settings.FolderMethod = FolderMethod;
        settings.OutputSubFolderName = OutputSubFolderName;
        settings.CustomOutputPath = CustomOutputPath;
        settings.CpuUsage = CpuUsage;
    }

    /// <summary>
    /// 프리셋 목록 바인딩을 새로고침하고 가능하면 원하는 프리셋을 다시 선택합니다.
    /// </summary>
    private void RefreshPresetCollection(string? preferredPresetName = null)
    {
        OnPropertyChanged(nameof(Presets));
        SelectedPreset = string.IsNullOrWhiteSpace(preferredPresetName)
            ? Presets.FirstOrDefault()
            : Presets.FirstOrDefault(preset => preset.Name == preferredPresetName) ?? Presets.FirstOrDefault();
    }

    /// <summary>
    /// 포맷 태그 컬렉션을 초기화하고 선택 변경을 지정된 처리기에 연결합니다.
    /// </summary>
    private void InitializeTagCollection(
        ObservableCollection<FormatTagViewModel> tags,
        IEnumerable<string> formats,
        Action<FormatTagViewModel> selectionChanged)
    {
        foreach (var format in formats)
        {
            var tag = new FormatTagViewModel(format);
            tag.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(FormatTagViewModel.IsSelected))
                    selectionChanged(tag);
            };
            tags.Add(tag);
        }
    }

    /// <summary>
    /// 대상 포맷 태그 그룹에 공통으로 적용되는 단일 선택 규칙을 처리합니다.
    /// </summary>
    private void HandleTargetTagSelectionChanged(
        ObservableCollection<FormatTagViewModel> tags,
        FormatTagViewModel changedTag,
        Action<FormatTagViewModel> onSelected,
        Action onAllDeselected)
    {
        if (_isSyncingTargetTags)
            return;

        if (changedTag.IsSelected)
        {
            // 태그는 단일 선택만 허용하므로 선택된 태그 외에는 모두 해제한다.
            ApplyExclusiveSelection(tags, changedTag);
            onSelected(changedTag);
            return;
        }

        if (tags.All(tag => !tag.IsSelected))
            onAllDeselected();
    }

    /// <summary>
    /// 선택된 태그만 남기고 같은 그룹의 나머지 태그 선택을 해제합니다.
    /// </summary>
    private void ApplyExclusiveSelection(
        ObservableCollection<FormatTagViewModel> tags,
        FormatTagViewModel selectedTag)
    {
        WithTargetTagSync(() =>
        {
            foreach (var other in tags.Where(tag => tag != selectedTag))
                other.IsSelected = false;
        });
    }

    /// <summary>
    /// 빈 선택을 허용하지 않는 그룹에서 반드시 선택되어야 하는 태그를 복구합니다.
    /// </summary>
    private void RestoreRequiredSelection(FormatTagViewModel tag)
    {
        WithTargetTagSync(() =>
        {
            tag.IsSelected = true;
        });
    }

    /// <summary>
    /// 재귀 동기화를 막는 보호 구간 안에서 태그 선택 갱신을 수행합니다.
    /// </summary>
    private void WithTargetTagSync(Action action)
    {
        // 태그 선택 변경이 다시 포맷 프로퍼티 변경을 유발하는 루프를 막는다.
        _isSyncingTargetTags = true;
        try
        {
            action();
        }
        finally
        {
            _isSyncingTargetTags = false;
        }
    }

    /// <summary>
    /// 서로 연관된 UI 속성들에 대해 일괄적으로 변경 알림을 발생시킵니다.
    /// </summary>
    private void NotifyPropertiesChanged(IEnumerable<string> propertyNames)
    {
        foreach (var propertyName in propertyNames)
            OnPropertyChanged(propertyName);
    }

    /// <summary>
    /// 선택된 대상 포맷을 일치하는 태그 항목의 선택 상태에 반영합니다.
    /// </summary>
    private static void SyncTagSelection(IEnumerable<FormatTagViewModel> tags, string? targetFormat)
    {
        foreach (var tag in tags)
            tag.IsSelected = tag.Format == targetFormat;
    }

    /// <summary>
    /// 저장된 배경색 문자열을 프리셋 편집기의 배경색 옵션으로 변환합니다.
    /// </summary>
    private static BackgroundColorOption ParseBackgroundColorOption(string value)
    {
        if (value.Equals("#FFFFFF", StringComparison.OrdinalIgnoreCase))
            return BackgroundColorOption.White;
        if (value.Equals("#000000", StringComparison.OrdinalIgnoreCase))
            return BackgroundColorOption.Black;
        return BackgroundColorOption.Custom;
    }

    /// <summary>
    /// 현재 배경색 옵션에 맞는 실제 저장 문자열을 결정합니다.
    /// </summary>
    private static string ResolveBackgroundColor(BackgroundColorOption option, string customColor)
    {
        return option switch
        {
            BackgroundColorOption.White => "#FFFFFF",
            BackgroundColorOption.Black => "#000000",
            _ => customColor
        };
    }

    /// <summary>
    /// GIF 오차 값을 허용 범위로 보정하고 반올림합니다.
    /// </summary>
    private static double CoerceGifErrorValue(double value)
    {
        double clamped = Math.Clamp(value, 0.0, 32.0);
        return Math.Round(clamped, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// 사용자가 입력한 GIF 오차 텍스트가 완전한 숫자일 때만 파싱합니다.
    /// </summary>
    private static bool TryParseGifErrorText(string? value, out double parsed)
    {
        parsed = 0.0;

        if (string.IsNullOrWhiteSpace(value) || value.EndsWith(".", StringComparison.Ordinal))
            return false;

        if (!double.TryParse(
                value,
                NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite,
                CultureInfo.InvariantCulture,
                out double raw))
        {
            return false;
        }

        parsed = CoerceGifErrorValue(raw);
        return true;
    }

    /// <summary>
    /// GIF 오차 값을 텍스트 박스와 같은 고정 소수점 형식으로 변환합니다.
    /// </summary>
    private static string FormatGifErrorValue(double value) =>
        CoerceGifErrorValue(value).ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>
    /// 검증된 GIF 오차 텍스트를 해당 숫자 속성과 정규화된 텍스트 필드에 반영합니다.
    /// </summary>
    private void ApplyGifErrorText(string value, bool isInterframe)
    {
        if (!TryParseGifErrorText(value, out double parsed))
            return;

        string formatted = FormatGifErrorValue(parsed);
        if (!value.Equals(formatted, StringComparison.Ordinal))
        {
            if (isInterframe)
                AnimationGifInterframeMaxErrorText = formatted;
            else
                AnimationGifInterpaletteMaxErrorText = formatted;
        }

        if (isInterframe)
            AnimationGifInterframeMaxError = parsed;
        else
            AnimationGifInterpaletteMaxError = parsed;
    }
}

public partial class FormatTagViewModel : ObservableObject
{
    public string Format { get; }

    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// 지정한 포맷 이름으로 선택 가능한 대상 포맷 태그를 생성합니다.
    /// </summary>
    public FormatTagViewModel(string format) => Format = format;
}
