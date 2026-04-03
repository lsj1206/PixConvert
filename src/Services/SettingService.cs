using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ModernWpf;
using PixConvert.Models;
using PixConvert.Services.Interfaces;

namespace PixConvert.Services;

/// <summary>
/// JSON 파일을 사용하여 앱 전역 설정을 관리하는 서비스 구현체입니다.
/// </summary>
public class SettingService : ISettingService
{
    private readonly ILogger<SettingService> _logger;
    private readonly ILanguageService _languageService;
    private readonly string _configPath;

    public AppSettings Settings { get; private set; } = new();

    public SettingService(ILogger<SettingService> logger, ILanguageService languageService)
    {
        _logger = logger;
        _languageService = languageService;

        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string folder = Path.Combine(appData, "PixConvert");

        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);

        _configPath = Path.Combine(folder, "settings.json");
    }

    /// <summary>
    /// 설정 파일을 로드하고 설정값을 애플리케이션에 즉시 적용합니다.
    /// 앱 시작 시 App.OnStartup에서 호출합니다.
    /// </summary>
    public async Task InitializeAsync()
    {
        await LoadAsync();

        // 언어 및 테마 자동 적용
        _languageService.ChangeLanguage(Settings.Language);

        ThemeManager.Current.ApplicationTheme = Settings.Theme switch
        {
            "Dark" => ApplicationTheme.Dark,
            _ => ApplicationTheme.Light
        };
    }

    /// <summary>
    /// 설정 파일을 비동기적으로 로드합니다.
    /// 파일이 없거나 손상된 경우 기본값으로 복구 후 파일에 저장합니다.
    /// </summary>
    public async Task LoadAsync()
    {
        try
        {
            if (!File.Exists(_configPath))
            {
                _logger.LogInformation(_languageService.GetString("Log_Setting_FileNotFound"));
                Settings = CreateDefaultSettings();
                _ = SaveAsync();
                return;
            }

            string json = await File.ReadAllTextAsync(_configPath);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json);

            if (loaded == null)
            {
                _logger.LogWarning(_languageService.GetString("Log_Setting_FileEmpty"));
                Settings = CreateDefaultSettings();
                _ = SaveAsync();
            }
            else
            {
                Settings = loaded;
                // 지원하지 않는 언어 코드는 시스템 언어로 복구
                var supportedLanguages = new[] { "ko-KR", "en-US" };
                if (!supportedLanguages.Contains(Settings.Language))
                {
                    Settings.Language = _languageService.GetSystemLanguage();
                    _ = SaveAsync();
                }
                _logger.LogInformation(_languageService.GetString("Log_Setting_FileLoadSuccess"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, _languageService.GetString("Log_Setting_FileLoadError"));
            Settings = CreateDefaultSettings();
            _ = SaveAsync();
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
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(Settings, options);
            await File.WriteAllTextAsync(_configPath, json);
            _logger.LogInformation(_languageService.GetString("Log_Setting_FileSaveSuccess"));
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, _languageService.GetString("Log_Setting_FileSaveError"));
            return false;
        }
    }

    /// <summary>
    /// settings.json이 없거나 손상되었을 때 호출되는 기본값 초기화 메서드입니다.
    /// Language는 시스템 언어를 감지하여 설정합니다.
    /// </summary>
    private AppSettings CreateDefaultSettings() => new()
    {
        Language = _languageService.GetSystemLanguage(),
        ConfirmDeletion = true,
        Theme = "Light"
    };
}
