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

    public ConvertPreset? ActivePreset { get; private set; }

    private readonly ILanguageService _languageService;

    public PresetService(ILogger<PresetService> logger, ILanguageService languageService)
    {
        _logger = logger;
        _languageService = languageService;

        // %AppData%/PixConvert/presets.json 경로 설정
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string folder = Path.Combine(appData, "PixConvert");

        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);

        _configPath = Path.Combine(folder, "presets.json");
    }

    /// <summary>
    /// 앱 시작 시 프리셋 파일을 로드하여 변환에 직접 사용할 ActivePreset을 초기화합니다.
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            if (!File.Exists(_configPath))
            {
                _logger.LogInformation(_languageService.GetString("Log_Preset_FileNotFound"));
                Config = CreateDefaultConfig();
                ActivePreset = null;
                return;
            }

            string json = await File.ReadAllTextAsync(_configPath);
            var loaded = JsonSerializer.Deserialize<PresetConfig>(json);

            if (loaded == null || loaded.Presets == null)
            {
                _logger.LogWarning(_languageService.GetString("Log_Preset_FileEmpty"));
                Config = CreateDefaultConfig();
                ActivePreset = null;
                return;
            }

            Config = loaded;

            // 로드된 데이터에서 LastSelectedPresetName을 찾아 ActivePreset으로 바인딩
            var active = Config.Presets.FirstOrDefault(p => p.Name == Config.LastSelectedPresetName);
            if (active != null)
            {
                ActivePreset = active;
                _logger.LogInformation(_languageService.GetString("Log_Preset_FileLoadSuccess") + $" (Active: {active.Name})");
            }
            else
            {
                _logger.LogWarning(_languageService.GetString("Log_Preset_NotFoundSelected"), Config.LastSelectedPresetName);
                ActivePreset = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, _languageService.GetString("Log_Preset_FileLoadError"));
            // 파싱 실패 또는 오류 발생 시 사용자의 원본 데이터가 증발하지 않도록 백업 수행
            try
            {
                if (File.Exists(_configPath))
                {
                    string backupPath = _configPath + ".bak";
                    File.Copy(_configPath, backupPath, true);
                    _logger.LogWarning("손상된 프리셋 파일을 백업했습니다: {BackupPath}", backupPath);
                }
            }
            catch { /* 백업 실패 예외는 진행을 위해 무시 */ }
            Config = CreateDefaultConfig();
            ActivePreset = null;
        }
    }

    /// <summary>
    /// 현재 설정을 파일에 비동기적으로 저장합니다.
    /// </summary>
    /// <returns>저장 성공 시 true, 실패 시 false.</returns>
    public async Task<bool> SaveAsync()
    {
        try
        {
            // 저장 전 디렉토리 존재 보장
            string? directory = Path.GetDirectoryName(_configPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(Config, options);
            await File.WriteAllTextAsync(_configPath, json);
            _logger.LogInformation(_languageService.GetString("Log_Preset_FileSaveSuccess"));
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, _languageService.GetString("Log_Preset_FileSaveError"));
            return false;
        }
    }

    /// <summary>
    /// 전달받은 변환 설정 값(데이터)이 올바른지 논리적 유효성을 검사합니다.
    /// </summary>
    public bool ValidPresetData(ConvertSettings settings, out string errorMessageKey)
    {
        _logger.LogInformation(_languageService.GetString("Log_Preset_ValidDataStart"));

        // 1. 설정 객체 자체가 Null인지 확인
        if (settings == null)
        {
            _logger.LogError(_languageService.GetString("Log_Preset_SettingsNull"));
            errorMessageKey = "Msg_Error_ConfigInvalid";
            return false;
        }

        // 2. 변환 품질(Quality) 범위 확인
        if (settings.StandardQuality < 1 || settings.StandardQuality > 100)
        {
            _logger.LogError(_languageService.GetString("Log_Preset_InvalidQuality"), settings.StandardQuality);
            errorMessageKey = "Msg_Error_ConfigInvalid";
            return false;
        }

        if (settings.AnimationQuality < 1 || settings.AnimationQuality > 100)
        {
            _logger.LogError(_languageService.GetString("Log_Preset_InvalidQuality"), settings.AnimationQuality);
            errorMessageKey = "Msg_Error_ConfigInvalid";
            return false;
        }

        if (settings.StandardPngCompressionLevel < 0 || settings.StandardPngCompressionLevel > 9)
        {
            _logger.LogError(_languageService.GetString("Log_Preset_InvalidQuality"), settings.StandardPngCompressionLevel);
            errorMessageKey = "Msg_Error_ConfigInvalid";
            return false;
        }

        if (settings.StandardAvifEncodingEffort < 0 || settings.StandardAvifEncodingEffort > 9)
        {
            _logger.LogError(_languageService.GetString("Log_Preset_InvalidQuality"), settings.StandardAvifEncodingEffort);
            errorMessageKey = "Msg_Error_ConfigInvalid";
            return false;
        }

        if (settings.AnimationGifInterframeMaxError < 0 || settings.AnimationGifInterframeMaxError > 32)
        {
            _logger.LogError(_languageService.GetString("Log_Preset_InvalidQuality"), settings.AnimationGifInterframeMaxError);
            errorMessageKey = "Msg_Error_ConfigInvalid";
            return false;
        }

        if (settings.AnimationGifInterpaletteMaxError < 0 || settings.AnimationGifInterpaletteMaxError > 32)
        {
            _logger.LogError(_languageService.GetString("Log_Preset_InvalidQuality"), settings.AnimationGifInterpaletteMaxError);
            errorMessageKey = "Msg_Error_ConfigInvalid";
            return false;
        }

        if (settings.AnimationWebpEncodingEffort < 0 || settings.AnimationWebpEncodingEffort > 6)
        {
            _logger.LogError(_languageService.GetString("Log_Preset_InvalidQuality"), settings.AnimationWebpEncodingEffort);
            errorMessageKey = "Msg_Error_ConfigInvalid";
            return false;
        }

        // 지원하는 확장자 포맷 배열
        var allowedStandard = new[] { "JPEG", "PNG", "BMP", "WEBP", "AVIF" };
        var allowedAnimation = new[] { "GIF", "WEBP" };

        // 3. 일반 이미지 변환 목표 확장자 확인
        if (string.IsNullOrEmpty(settings.StandardTargetFormat) || !allowedStandard.Contains(settings.StandardTargetFormat, StringComparer.OrdinalIgnoreCase))
        {
            _logger.LogError(_languageService.GetString("Log_Preset_InvalidStdFormat"), settings.StandardTargetFormat);
            errorMessageKey = "Msg_Error_ConfigInvalid";
            return false;
        }

        // 4. 애니메이션(움짤) 이미지 타겟 포맷 검증
        if (!string.IsNullOrWhiteSpace(settings.AnimationTargetFormat) &&
            !allowedAnimation.Contains(settings.AnimationTargetFormat, StringComparer.OrdinalIgnoreCase))
        {
            _logger.LogError(_languageService.GetString("Log_Preset_InvalidAnimFormat"), settings.AnimationTargetFormat);
            errorMessageKey = "Msg_Error_ConfigInvalid";
            return false;
        }

        // 5. 열거형 옵션 및 배경색 HEX 유효성 확인
        if (!Enum.IsDefined(typeof(CpuUsageOption), settings.CpuUsage) ||
            !Enum.IsDefined(typeof(SaveLocationType), settings.SaveLocation) ||
            !Enum.IsDefined(typeof(SaveFolderMethod), settings.FolderMethod) ||
            !Enum.IsDefined(typeof(OverwritePolicy), settings.OverwritePolicy) ||
            !Enum.IsDefined(typeof(JpegChromaSubsamplingMode), settings.StandardJpegChromaSubsampling) ||
            !Enum.IsDefined(typeof(PngFilterMode), settings.StandardPngFilter) ||
            !Enum.IsDefined(typeof(AvifChromaSubsamplingMode), settings.StandardAvifChromaSubsampling) ||
            !Enum.IsDefined(typeof(AvifBitDepthMode), settings.StandardAvifBitDepth) ||
            !Enum.IsDefined(typeof(GifPalettePreset), settings.AnimationGifPalettePreset) ||
            !Enum.IsDefined(typeof(WebpPresetMode), settings.AnimationWebpPreset))
        {
            _logger.LogError(_languageService.GetString("Log_Preset_InvalidEnum"));
            errorMessageKey = "Msg_Error_ConfigInvalid";
            return false;
        }

        // 배경색 HEX 포맷 검증 (#RRGGBB 또는 #AARRGGBB)
        if (string.IsNullOrWhiteSpace(settings.StandardBackgroundColor) || !System.Text.RegularExpressions.Regex.IsMatch(settings.StandardBackgroundColor, "^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{8})$"))
        {
            _logger.LogError(_languageService.GetString("Log_Preset_InvalidBgColor"), settings.StandardBackgroundColor);
            errorMessageKey = "Msg_Error_ConfigInvalid";
            return false;
        }

        // 6. 사용자 지정 출력 경로 확인
        if (settings.SaveLocation == SaveLocationType.Custom && string.IsNullOrWhiteSpace(settings.CustomOutputPath))
        {
            _logger.LogError(_languageService.GetString("Log_Preset_EmptyCustomPath"));
            errorMessageKey = "Msg_Error_ConfigInvalid";
            return false;
        }

        // 7. 하위 폴더 이름 유효성 검사
        if (settings.FolderMethod == SaveFolderMethod.CreateFolder)
        {
            if (string.IsNullOrWhiteSpace(settings.OutputSubFolderName))
            {
                _logger.LogError(_languageService.GetString("Log_Preset_SubFolderEmpty"));
                errorMessageKey = "Msg_Error_ConfigInvalid";
                return false;
            }

            var invalidChars = Path.GetInvalidFileNameChars();
            if (settings.OutputSubFolderName.IndexOfAny(invalidChars) >= 0)
            {
                _logger.LogError(_languageService.GetString("Log_Preset_SubFolderInvalid"), settings.OutputSubFolderName);
                errorMessageKey = "Msg_Error_ConfigInvalid";
                return false;
            }
        }

        _logger.LogInformation(_languageService.GetString("Log_Preset_DataValid"));
        errorMessageKey = string.Empty;
        return true;
    }

    /// <summary>
    /// 활성 프리셋을 갱신합니다.
    /// </summary>
    public void UpdateActivePreset(ConvertPreset preset)
    {
        ActivePreset = preset;
        Config.LastSelectedPresetName = preset.Name;
    }


    /// <summary>
    /// 프리셋 설정 파일(JSON)을 완전히 처음부터 새로 만들어야 하거나, 손상되었을 때 호출되는 기본 초기화 메서드입니다.
    /// </summary>
    private PresetConfig CreateDefaultConfig()
    {
        var config = new PresetConfig();
        // 기본 1번 프리셋 하나를 세팅하여 프로그램 안정성을 확보
        config.Presets.Add(new ConvertPreset { Name = "Preset_1", Settings = new ConvertSettings() });
        config.LastSelectedPresetName = "Preset_1";
        return config;
    }

    /// <summary>
    /// 새로운 변환 프리셋을 목록에 추가합니다.
    /// </summary>
    /// <param name="name">생성할 프리셋의 이름</param>
    /// <param name="settings">복사해서 저장할 대상 ConvertSettings 변환 옵션 정보</param>
    public void AddPreset(string name, ConvertSettings settings)
    {
        // 중복되는 이름이 이미 있을 경우 추가를 무시함
        if (Config.Presets.Any(p => p.Name == name)) return;

        Config.Presets.Add(new ConvertPreset { Name = name, Settings = CopySettings(settings) });
    }

    /// <summary>
    /// 이름으로 검색하여 일치하는 프리셋을 목록에서 삭제합니다.
    /// </summary>
    public void RemovePreset(string name)
    {
        var preset = Config.Presets.FirstOrDefault(p => p.Name == name);
        if (preset != null)
        {
            Config.Presets.Remove(preset);
        }
    }

    /// <summary>
    /// 프리셋의 이름을 새로운 이름으로 변경합니다.
    /// </summary>
    public void RenamePreset(string oldName, string newName)
    {
        var preset = Config.Presets.FirstOrDefault(p => p.Name == oldName);
        // 바꿀 이름의 프리셋이 구비되어 있고, 변경하려는 새 이름이 중복되지 않는 경우에만 실행
        if (preset != null && !Config.Presets.Any(p => p.Name == newName))
        {
            preset.Name = newName;
            // 마지막으로 선택된 이름도 이전 이름과 동일하다면 최신으로 업데이트
            if (Config.LastSelectedPresetName == oldName)
                Config.LastSelectedPresetName = newName;
        }
    }

    /// <summary>
    /// 기존 프리셋의 설정을 그대로 복사하여 다른 이름으로 새 프리셋을 생성합니다.
    /// </summary>
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

    /// <summary>
    /// 새로운 프리셋이나 복제 생성 시, 참조값이 아닌 완전한 새 메모리 객체 할당(깊은 복사)을 수행합니다.
    /// </summary>
    private ConvertSettings CopySettings(ConvertSettings source)
    {
        // 간단한 JSON 직렬화/역직렬화 트릭을 통한 깊은 복사(Deep Copy)를 수행
        string json = JsonSerializer.Serialize(source);
        return JsonSerializer.Deserialize<ConvertSettings>(json) ?? new ConvertSettings();
    }
}
