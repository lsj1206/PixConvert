using System;
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
    private readonly IPresetService _presetService;
    private bool _isSyncingTargetTags;

    public ObservableCollection<ConvertPreset> Presets => new(_presetService.Config.Presets);

    [ObservableProperty] private ConvertPreset? _selectedPreset;
    [ObservableProperty] private string _presetNameEdit = string.Empty;
    [ObservableProperty] private string _standardTargetFormat = "JPEG";
    [ObservableProperty] private string? _animationTargetFormat = "GIF";
    [ObservableProperty] private int _standardQuality = 85;
    [ObservableProperty] private bool _standardLossless;
    [ObservableProperty] private JpegChromaSubsamplingMode _standardJpegChromaSubsampling = JpegChromaSubsamplingMode.Auto;
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

    public ConvertSettingViewModel(
        ILanguageService languageService,
        ILogger<ConvertSettingViewModel> logger,
        IPresetService presetService)
        : base(languageService, logger)
    {
        _presetService = presetService;

        InitializeTags();

        CreatePresetCommand = new RelayCommand(CreatePreset);
        CopyPresetCommand = new RelayCommand(CopyPreset, () => SelectedPreset != null);
        RemovePresetCommand = new RelayCommand(RemovePreset, () => SelectedPreset != null && Presets.Count > 1);
        RenamePresetCommand = new RelayCommand(RenamePreset, () => SelectedPreset != null && !string.IsNullOrWhiteSpace(PresetNameEdit));
        ChangeOutputPathCommand = new RelayCommand(ChangeOutputPath);

        var lastPreset = Presets.FirstOrDefault(p => p.Name == _presetService.Config.LastSelectedPresetName) ?? Presets.FirstOrDefault();
        SelectedPreset = lastPreset;
    }

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

    partial void OnStandardQualityChanged(int value)
    {
        int coerced = Math.Clamp(value, 1, 100);
        if (value != coerced)
            StandardQuality = coerced;
    }

    partial void OnAnimationQualityChanged(int value)
    {
        int coerced = Math.Clamp(value, 1, 100);
        if (value != coerced)
            AnimationQuality = coerced;
    }

    partial void OnStandardPngCompressionLevelChanged(int value)
    {
        int coerced = Math.Clamp(value, 0, 9);
        if (value != coerced)
            StandardPngCompressionLevel = coerced;
    }

    partial void OnStandardAvifEncodingEffortChanged(int value)
    {
        int coerced = Math.Clamp(value, 0, 9);
        if (value != coerced)
            StandardAvifEncodingEffort = coerced;
    }

    partial void OnAnimationWebpEncodingEffortChanged(int value)
    {
        int coerced = Math.Clamp(value, 0, 6);
        if (value != coerced)
            AnimationWebpEncodingEffort = coerced;
    }

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

    partial void OnAnimationGifInterframeMaxErrorTextChanged(string value)
    {
        ApplyGifErrorText(value, true);
    }

    partial void OnAnimationGifInterpaletteMaxErrorTextChanged(string value)
    {
        ApplyGifErrorText(value, false);
    }

    partial void OnStandardTargetFormatChanged(string value) => OnTargetFormatsChanged();
    partial void OnAnimationTargetFormatChanged(string? value) => OnTargetFormatsChanged();

    partial void OnStandardLosslessChanged(bool value)
    {
        OnPropertyChanged(nameof(StandardShowAvifChromaSubsampling));
        OnPropertyChanged(nameof(StandardShowQuality));
        OnPropertyChanged(nameof(StandardCanEditAvifChromaSubsampling));
    }

    partial void OnAnimationLosslessChanged(bool value)
    {
        OnPropertyChanged(nameof(AnimationShowQuality));
        OnPropertyChanged(nameof(AnimationShowWebpPreset));
        OnPropertyChanged(nameof(AnimationShowWebpPreserveTransparentPixels));
    }

    private void InitializeTags()
    {
        string[] standardTargets = ["JPEG", "PNG", "BMP", "WEBP", "AVIF"];
        string[] animationTargets = ["GIF", "WEBP"];

        foreach (var format in standardTargets)
        {
            var tag = new FormatTagViewModel(format);
            tag.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(FormatTagViewModel.IsSelected))
                    HandleStandardTagSelectionChanged(tag);
            };
            StandardTargetTags.Add(tag);
        }

        foreach (var format in animationTargets)
        {
            var tag = new FormatTagViewModel(format);
            tag.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(FormatTagViewModel.IsSelected))
                    HandleAnimationTagSelectionChanged(tag);
            };
            AnimationTargetTags.Add(tag);
        }
    }

    private void LoadFromSettings(ConvertSettings settings)
    {
        StandardTargetFormat = string.IsNullOrWhiteSpace(settings.StandardTargetFormat)
            ? "JPEG"
            : settings.StandardTargetFormat;
        AnimationTargetFormat = settings.AnimationTargetFormat;

        StandardQuality = settings.StandardQuality;
        StandardLossless = settings.StandardLossless;
        StandardJpegChromaSubsampling = settings.StandardJpegChromaSubsampling;
        StandardPngCompressionLevel = settings.StandardPngCompressionLevel;
        StandardAvifChromaSubsampling = settings.StandardAvifChromaSubsampling;
        StandardAvifEncodingEffort = settings.StandardAvifEncodingEffort;
        StandardAvifBitDepth = settings.StandardAvifBitDepth;
        StandardCustomBackgroundColor = settings.StandardBackgroundColor ?? "#FFFFFF";
        StandardBgColorOption = ParseBackgroundColorOption(StandardCustomBackgroundColor);

        AnimationQuality = settings.AnimationQuality;
        AnimationLossless = settings.AnimationLossless;
        AnimationWebpEncodingEffort = settings.AnimationWebpEncodingEffort;
        AnimationWebpPreset = settings.AnimationWebpPreset;
        AnimationWebpPreserveTransparentPixels = settings.AnimationWebpPreserveTransparentPixels;
        AnimationGifPalettePreset = settings.AnimationGifPalettePreset;
        AnimationGifInterframeMaxError = settings.AnimationGifInterframeMaxError;
        AnimationGifInterpaletteMaxError = settings.AnimationGifInterpaletteMaxError;
        AnimationGifInterframeMaxErrorText = FormatGifErrorValue(AnimationGifInterframeMaxError);
        AnimationGifInterpaletteMaxErrorText = FormatGifErrorValue(AnimationGifInterpaletteMaxError);

        OverwritePolicy = settings.OverwritePolicy;
        SaveLocation = settings.SaveLocation;
        FolderMethod = settings.FolderMethod;
        OutputSubFolderName = settings.OutputSubFolderName ?? "PixConvert";
        CustomOutputPath = settings.CustomOutputPath ?? string.Empty;
        CpuUsage = settings.CpuUsage;

        SyncTargetTags();
    }

    public void SyncToSettings()
    {
        if (SelectedPreset == null)
            return;

        var settings = SelectedPreset.Settings;
        settings.StandardTargetFormat = StandardTargetFormat;
        settings.AnimationTargetFormat = AnimationTargetFormat;

        settings.StandardQuality = StandardQuality;
        settings.StandardLossless = StandardLossless;
        settings.StandardJpegChromaSubsampling = StandardJpegChromaSubsampling;
        settings.StandardPngCompressionLevel = StandardPngCompressionLevel;
        settings.StandardAvifChromaSubsampling = StandardAvifChromaSubsampling;
        settings.StandardAvifEncodingEffort = StandardAvifEncodingEffort;
        settings.StandardAvifBitDepth = StandardAvifBitDepth;
        settings.StandardBackgroundColor = ResolveBackgroundColor(StandardBgColorOption, StandardCustomBackgroundColor);

        settings.AnimationQuality = AnimationQuality;
        settings.AnimationLossless = AnimationLossless;
        settings.AnimationWebpEncodingEffort = AnimationWebpEncodingEffort;
        settings.AnimationWebpPreset = AnimationWebpPreset;
        settings.AnimationWebpPreserveTransparentPixels = AnimationWebpPreserveTransparentPixels;
        settings.AnimationGifPalettePreset = AnimationGifPalettePreset;
        settings.AnimationGifInterframeMaxError = AnimationGifInterframeMaxError;
        settings.AnimationGifInterpaletteMaxError = AnimationGifInterpaletteMaxError;

        settings.OverwritePolicy = OverwritePolicy;
        settings.SaveLocation = SaveLocation;
        settings.FolderMethod = FolderMethod;
        settings.OutputSubFolderName = OutputSubFolderName;
        settings.CustomOutputPath = CustomOutputPath;
        settings.CpuUsage = CpuUsage;
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
        if (SelectedPreset == null)
            return;

        string newName = $"{SelectedPreset.Name}_Copy";
        _presetService.CopyPreset(SelectedPreset.Name, newName);
        OnPropertyChanged(nameof(Presets));
        SelectedPreset = Presets.FirstOrDefault(p => p.Name == newName);
    }

    private void RemovePreset()
    {
        if (SelectedPreset == null)
            return;

        _presetService.RemovePreset(SelectedPreset.Name);
        OnPropertyChanged(nameof(Presets));
        SelectedPreset = Presets.FirstOrDefault();
    }

    private void RenamePreset()
    {
        if (SelectedPreset == null || string.IsNullOrWhiteSpace(PresetNameEdit))
            return;

        string oldName = SelectedPreset.Name;
        _presetService.RenamePreset(oldName, PresetNameEdit);
        OnPropertyChanged(nameof(Presets));
        SelectedPreset = Presets.FirstOrDefault(p => p.Name == PresetNameEdit);
    }

    private void ChangeOutputPath()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = _languageService.GetString("Dlg_Title_SelectOutputPath")
        };

        if (dialog.ShowDialog() == true)
            CustomOutputPath = dialog.FolderName;
    }

    private void OnTargetFormatsChanged()
    {
        SyncTargetTags();

        OnPropertyChanged(nameof(StandardShowLossless));
        OnPropertyChanged(nameof(StandardShowQuality));
        OnPropertyChanged(nameof(StandardShowBackgroundColor));
        OnPropertyChanged(nameof(StandardShowJpegChromaSubsampling));
        OnPropertyChanged(nameof(StandardShowPngCompression));
        OnPropertyChanged(nameof(StandardShowAvifChromaSubsampling));
        OnPropertyChanged(nameof(StandardCanEditAvifChromaSubsampling));
        OnPropertyChanged(nameof(StandardShowAvifEncodingEffort));
        OnPropertyChanged(nameof(StandardShowAvifBitDepth));
        OnPropertyChanged(nameof(ShowAnimationOptionsSection));
        OnPropertyChanged(nameof(AnimationShowLossless));
        OnPropertyChanged(nameof(AnimationShowQuality));
        OnPropertyChanged(nameof(AnimationShowGifPalettePreset));
        OnPropertyChanged(nameof(AnimationShowGifInterframeMaxError));
        OnPropertyChanged(nameof(AnimationShowGifInterpaletteMaxError));
        OnPropertyChanged(nameof(AnimationShowWebpEncodingEffort));
        OnPropertyChanged(nameof(AnimationShowWebpPreset));
        OnPropertyChanged(nameof(AnimationShowWebpPreserveTransparentPixels));
    }

    private void HandleStandardTagSelectionChanged(FormatTagViewModel tag)
    {
        if (_isSyncingTargetTags)
            return;

        if (tag.IsSelected)
        {
            _isSyncingTargetTags = true;
            try
            {
                foreach (var other in StandardTargetTags.Where(t => t != tag))
                    other.IsSelected = false;
            }
            finally
            {
                _isSyncingTargetTags = false;
            }

            StandardTargetFormat = tag.Format;
            return;
        }

        if (StandardTargetTags.All(t => !t.IsSelected))
        {
            _isSyncingTargetTags = true;
            try
            {
                tag.IsSelected = true;
            }
            finally
            {
                _isSyncingTargetTags = false;
            }
        }
    }

    private void HandleAnimationTagSelectionChanged(FormatTagViewModel tag)
    {
        if (_isSyncingTargetTags)
            return;

        if (tag.IsSelected)
        {
            _isSyncingTargetTags = true;
            try
            {
                foreach (var other in AnimationTargetTags.Where(t => t != tag))
                    other.IsSelected = false;
            }
            finally
            {
                _isSyncingTargetTags = false;
            }

            AnimationTargetFormat = tag.Format;
            return;
        }

        if (AnimationTargetTags.All(t => !t.IsSelected))
            AnimationTargetFormat = null;
    }

    private void SyncTargetTags()
    {
        if (_isSyncingTargetTags)
            return;

        _isSyncingTargetTags = true;
        try
        {
            foreach (var tag in StandardTargetTags)
                tag.IsSelected = tag.Format == StandardTargetFormat;

            foreach (var tag in AnimationTargetTags)
                tag.IsSelected = tag.Format == AnimationTargetFormat;
        }
        finally
        {
            _isSyncingTargetTags = false;
        }
    }

    private static BackgroundColorOption ParseBackgroundColorOption(string value)
    {
        if (value.Equals("#FFFFFF", StringComparison.OrdinalIgnoreCase))
            return BackgroundColorOption.White;
        if (value.Equals("#000000", StringComparison.OrdinalIgnoreCase))
            return BackgroundColorOption.Black;
        return BackgroundColorOption.Custom;
    }

    private static string ResolveBackgroundColor(BackgroundColorOption option, string customColor)
    {
        return option switch
        {
            BackgroundColorOption.White => "#FFFFFF",
            BackgroundColorOption.Black => "#000000",
            _ => customColor
        };
    }

    private static double CoerceGifErrorValue(double value)
    {
        double clamped = Math.Clamp(value, 0.0, 32.0);
        return Math.Round(clamped, 2, MidpointRounding.AwayFromZero);
    }

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

    private static string FormatGifErrorValue(double value) =>
        CoerceGifErrorValue(value).ToString("0.##", CultureInfo.InvariantCulture);

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

    public FormatTagViewModel(string format) => Format = format;
}
