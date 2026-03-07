using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PixConvert.Models;
using PixConvert.Services.Interfaces;

namespace PixConvert.Services;

/// <summary>
/// JSON 파일을 사용하여 사용자 설정 및 변환 프리셋을 관리하는 서비스 구현체입니다.
/// </summary>
public class PresetService : IPresetService
{
    private readonly ILogger<PresetService> _logger;
    private readonly string _configPath;

    public PresetConfig Config { get; private set; } = new();

    private readonly ILanguageService _languageService;

    public PresetService(ILogger<PresetService> logger, ILanguageService languageService)
    {
        _logger = logger;
        _languageService = languageService;

        // %AppData%/PixConvert/settings.json 경로 설정
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string folder = Path.Combine(appData, "PixConvert");

        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);

        _configPath = Path.Combine(folder, "presets.json");
    }

    /// <summary>
    /// 설정 파일을 비동기적으로 로드합니다. 파일이 없거나 오류 발생 시 기본값을 사용합니다.
    /// </summary>
    public async Task LoadAsync()
    {
        try
        {
            if (!File.Exists(_configPath))
            {
                _logger.LogInformation(_languageService.GetString("Log_Preset_FileNotFound"));
                Config = CreateDefaultConfig();
                return;
            }

            string json = await File.ReadAllTextAsync(_configPath);
            var loaded = JsonSerializer.Deserialize<PresetConfig>(json);

            if (loaded == null)
            {
                _logger.LogWarning(_languageService.GetString("Log_Preset_FileEmpty"));
                Config = CreateDefaultConfig();
            }
            else
            {
                Config = loaded;
                if (!ValidPresetFile())
                {
                    _logger.LogWarning(_languageService.GetString("Log_Preset_FileCorrupted"));
                    _ = SaveAsync(); // 복구된 설정 비동기 저장
                }
                _logger.LogInformation(_languageService.GetString("Log_Preset_FileLoadSuccess"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, _languageService.GetString("Log_Preset_FileLoadError"));
            Config = CreateDefaultConfig();
        }
    }

    /// <summary>
    /// 현재 설정을 파일에 비동기적으로 저장합니다.
    /// </summary>
    public async Task SaveAsync()
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(Config, options);
            await File.WriteAllTextAsync(_configPath, json);
            _logger.LogInformation(_languageService.GetString("Log_Preset_FileSaveSuccess"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, _languageService.GetString("Log_Preset_FileSaveError"));
        }
    }


    public bool ValidPresetFile()
    {
        _logger.LogInformation(_languageService.GetString("Log_Preset_ValidFileStart"));
        bool isModified = false;

        if (Config.Presets == null)
        {
            _logger.LogWarning(_languageService.GetString("Log_Preset_ListNull"));
            Config.Presets = new();
            isModified = true;
        }

        if (Config.Presets.Count == 0)
        {
            _logger.LogWarning(_languageService.GetString("Log_Preset_ListEmpty"));
            Config.Presets.Add(new ConvertPreset { Name = "Preset_1", Settings = new ConvertSettings() });
            Config.LastSelectedPresetName = "Preset_1";
            isModified = true;
        }

        if (!Config.Presets.Any(p => p.Name == Config.LastSelectedPresetName))
        {
            _logger.LogWarning(_languageService.GetString("Log_Preset_NotFoundSelected"), Config.LastSelectedPresetName);
            Config.LastSelectedPresetName = Config.Presets.First().Name;
            isModified = true;
        }

        if (isModified)
            _logger.LogWarning(_languageService.GetString("Log_Preset_StructureModified"));
        else
            _logger.LogInformation(_languageService.GetString("Log_Preset_StructureValid"));

        return !isModified;
    }

    public bool ValidPresetData(out string errorMessageKey)
    {
        _logger.LogInformation(_languageService.GetString("Log_Preset_ValidDataStart"));
        var currentPreset = Config.Presets.FirstOrDefault(p => p.Name == Config.LastSelectedPresetName);
        var settings = currentPreset?.Settings;

        if (settings == null)
        {
            _logger.LogError(_languageService.GetString("Log_Preset_SettingsNull"));
            errorMessageKey = "Msg_Error_ConfigInvalid";
            return false;
        }

        if (settings.Quality < 1 || settings.Quality > 100)
        {
            _logger.LogError(_languageService.GetString("Log_Preset_InvalidQuality"), settings.Quality);
            errorMessageKey = "Msg_Error_ConfigInvalid";
            return false;
        }

        var allowedStandard = new[] { "JPEG", "PNG", "BMP", "WEBP", "AVIF" };
        var allowedAnimation = new[] { "GIF", "WEBP" };

        if (string.IsNullOrEmpty(settings.StandardTargetFormat) || !allowedStandard.Contains(settings.StandardTargetFormat, StringComparer.OrdinalIgnoreCase))
        {
            _logger.LogError(_languageService.GetString("Log_Preset_InvalidStdFormat"), settings.StandardTargetFormat);
            errorMessageKey = "Msg_Error_ConfigInvalid";
            return false;
        }

        if (string.IsNullOrEmpty(settings.AnimationTargetFormat) || !allowedAnimation.Contains(settings.AnimationTargetFormat, StringComparer.OrdinalIgnoreCase))
        {
            _logger.LogError(_languageService.GetString("Log_Preset_InvalidAnimFormat"), settings.AnimationTargetFormat);
            errorMessageKey = "Msg_Error_ConfigInvalid";
            return false;
        }

        if (!Enum.IsDefined(typeof(CpuUsageOption), settings.CpuUsage) ||
            !Enum.IsDefined(typeof(OutputPathType), settings.OutputType) ||
            !Enum.IsDefined(typeof(BackgroundColorOption), settings.BgColorOption) ||
            !Enum.IsDefined(typeof(OverwritePolicy), settings.OverwriteSide))
        {
            _logger.LogError(_languageService.GetString("Log_Preset_InvalidEnum"));
            errorMessageKey = "Msg_Error_ConfigInvalid";
            return false;
        }

        _logger.LogInformation(_languageService.GetString("Log_Preset_DataValid"));
        errorMessageKey = string.Empty;
        return true;
    }

    public void AddPreset(string name, ConvertSettings settings)
    {
        if (Config.Presets.Any(p => p.Name == name)) return;

        Config.Presets.Add(new ConvertPreset { Name = name, Settings = CopySettings(settings) });
    }

    public void RemovePreset(string name)
    {
        var preset = Config.Presets.FirstOrDefault(p => p.Name == name);
        if (preset != null)
        {
            Config.Presets.Remove(preset);
        }
    }

    public void RenamePreset(string oldName, string newName)
    {
        var preset = Config.Presets.FirstOrDefault(p => p.Name == oldName);
        if (preset != null && !Config.Presets.Any(p => p.Name == newName))
        {
            preset.Name = newName;
            if (Config.LastSelectedPresetName == oldName)
                Config.LastSelectedPresetName = newName;
        }
    }

    public void CopyPreset(string sourceName, string newName)
    {
        var source = Config.Presets.FirstOrDefault(p => p.Name == sourceName);
        if (source != null && !Config.Presets.Any(p => p.Name == newName))
        {
            Config.Presets.Add(new ConvertPreset
            {
                Name = newName,
                Settings = CopySettings(source.Settings)
            });
        }
    }

    private PresetConfig CreateDefaultConfig()
    {
        var config = new PresetConfig();
        // 기본 프리셋 하나 추가
        config.Presets.Add(new ConvertPreset { Name = "Preset_1", Settings = new ConvertSettings() });
        config.LastSelectedPresetName = "Preset_1";
        return config;
    }

    private ConvertSettings CopySettings(ConvertSettings source)
    {
        // 간단한 직렬화/역직렬화를 통한 깊은 복사 (또는 수동 매핑)
        string json = JsonSerializer.Serialize(source);
        return JsonSerializer.Deserialize<ConvertSettings>(json) ?? new ConvertSettings();
    }
}
