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
                // ValidPresetFile 내부에서 구조 이상 감지 시 로그 및 자동 복구 처리
                if (!ValidPresetFile())
                    _ = SaveAsync(); // 복구된 설정 즉시 파일에 반영
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
    /// <returns>저장 성공 시 true, 실패 시 false. 알림 처리는 호출자(ViewModel)에서 수행합니다.</returns>
    public async Task<bool> SaveAsync()
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(Config, options);
            await File.WriteAllTextAsync(_configPath, json);
            _logger.LogInformation(_languageService.GetString("Log_Preset_FileSaveSuccess"));
            return true; // 저장 성공
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, _languageService.GetString("Log_Preset_FileSaveError"));
            return false; // 저장 실패
        }
    }


    /// <summary>
    /// 프리셋(presets.json) 파일 구조의 무결성을 검증하고, 오류가 있다면 기본값으로 복원합니다.
    /// </summary>
    /// <returns>구조 변경이 발생하지 않았다면 true, 복구를 수행했다면 false 반환</returns>
    public bool ValidPresetFile()
    {
        _logger.LogInformation(_languageService.GetString("Log_Preset_ValidFileStart"));
        bool isModified = false;

        // 1. 프리셋 리스트 자체가 존재하지 않는 경우 복구
        if (Config.Presets == null)
        {
            _logger.LogWarning(_languageService.GetString("Log_Preset_ListNull"));
            Config.Presets = new();
            isModified = true;
        }

        // 2. 프리셋 리스트가 비어있는 경우 기본 프리셋 생성
        if (Config.Presets.Count == 0)
        {
            _logger.LogWarning(_languageService.GetString("Log_Preset_ListEmpty"));
            Config.Presets.Add(new ConvertPreset { Name = "Preset_1", Settings = new ConvertSettings() });
            Config.LastSelectedPresetName = "Preset_1";
            isModified = true;
        }

        // 3. 마지막으로 선택된 프리셋 이름이 리스트에 존재하지 않을 경우 첫 번째 항목으로 재지정
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

        // 배열이 수정되지 않고 완벽하게 일치해야만 true(정상 통과)를 반환
        return !isModified;
    }

    /// <summary>
    /// 현재 선택된 프리셋의 실제 변환 설정 값(데이터)이 올바른지 논리적 유효성을 미리 검사합니다.
    /// </summary>
    /// <param name="errorMessageKey">오류 발생 시 UI(스낵바)에 출력할 다국어 메시지 키</param>
    /// <returns>검증 성공 시 true, 규격 미달 시 false 반환</returns>
    public bool ValidPresetData(out string errorMessageKey)
    {
        _logger.LogInformation(_languageService.GetString("Log_Preset_ValidDataStart"));
        var currentPreset = Config.Presets.FirstOrDefault(p => p.Name == Config.LastSelectedPresetName);
        var settings = currentPreset?.Settings;

        // 1. 설정 객체 자체가 Null인지 확인
        if (settings == null)
        {
            _logger.LogError(_languageService.GetString("Log_Preset_SettingsNull"));
            errorMessageKey = "Msg_Error_ConfigInvalid";
            return false;
        }

        // 2. 변환 품질(Quality) 범위가 1~100 사이를 벗어났는지 확인
        if (settings.Quality < 1 || settings.Quality > 100)
        {
            _logger.LogError(_languageService.GetString("Log_Preset_InvalidQuality"), settings.Quality);
            errorMessageKey = "Msg_Error_ConfigInvalid";
            return false;
        }

        // 지원하는 확장자 포맷 배열
        var allowedStandard = new[] { "JPEG", "PNG", "BMP", "WEBP", "AVIF" };
        var allowedAnimation = new[] { "GIF", "WEBP", "AVIF" };

        // 3. 일반 이미지 변환 목표 확장자가 규격(리스트)에 맞는지 확인
        if (string.IsNullOrEmpty(settings.StandardTargetFormat) || !allowedStandard.Contains(settings.StandardTargetFormat, StringComparer.OrdinalIgnoreCase))
        {
            _logger.LogError(_languageService.GetString("Log_Preset_InvalidStdFormat"), settings.StandardTargetFormat);
            errorMessageKey = "Msg_Error_ConfigInvalid";
            return false;
        }

        // 4. 애니메이션(움짤) 이미지 타겟 포맷 검증
        if (string.IsNullOrEmpty(settings.AnimationTargetFormat) || !allowedAnimation.Contains(settings.AnimationTargetFormat, StringComparer.OrdinalIgnoreCase))
        {
            _logger.LogError(_languageService.GetString("Log_Preset_InvalidAnimFormat"), settings.AnimationTargetFormat);
            errorMessageKey = "Msg_Error_ConfigInvalid";
            return false;
        }

        // 5. 열거형(Enum: CPU 사용량 설정, 경로, 배경색, 덮어쓰기 여부) 옵션이 유효한지 확인
        if (!Enum.IsDefined(typeof(CpuUsageOption), settings.CpuUsage) ||
            !Enum.IsDefined(typeof(SaveLocationType), settings.SaveLocation) ||
            !Enum.IsDefined(typeof(SaveFolderMethod), settings.FolderMethod) ||
            !Enum.IsDefined(typeof(BackgroundColorOption), settings.BgColorOption) ||
            !Enum.IsDefined(typeof(OverwritePolicy), settings.OverwritePolicy))
        {
            _logger.LogError(_languageService.GetString("Log_Preset_InvalidEnum"));
            errorMessageKey = "Msg_Error_ConfigInvalid";
            return false;
        }

        // 6. 사용자 지정 출력 경로(Custom) 선택 시 경로가 비어 있는지 확인
        if (settings.SaveLocation == SaveLocationType.Custom && string.IsNullOrWhiteSpace(settings.CustomOutputPath))
        {
            _logger.LogError(_languageService.GetString("Log_Preset_EmptyCustomPath"));
            errorMessageKey = "Msg_Error_ConfigInvalid";
            return false;
        }

        // 7. 하위 폴더 생성 선택 시 폴더 이름 유효성 검사
        if (settings.FolderMethod == SaveFolderMethod.CreateFolder)
        {
            if (string.IsNullOrWhiteSpace(settings.OutputSubFolderName))
            {
                _logger.LogError(_languageService.GetString("Log_Preset_SubFolderEmpty"));
                errorMessageKey = "Msg_Error_ConfigInvalid";
                return false;
            }

            // 파일명/폴더명에 사용할 수 없는 문자 포함 여부 체크 (v3: 토큰 예외 없이 전체 금지 문자 체크)
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
    /// 새로운 프리셋이나 복제 생성 시, 참조값이 아닌 완전한 새 메모리 객체 할당(깊은 복사)을 수행합니다.
    /// </summary>
    private ConvertSettings CopySettings(ConvertSettings source)
    {
        // 간단한 JSON 직렬화/역직렬화 트릭을 통한 깊은 복사(Deep Copy)를 수행
        string json = JsonSerializer.Serialize(source);
        return JsonSerializer.Deserialize<ConvertSettings>(json) ?? new ConvertSettings();
    }
}
