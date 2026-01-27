using System;
using System.Linq;
using System.Globalization;
using System.Windows;

namespace PixConvert.Services;

/// <summary>
/// 애플리케이션 언어 관리 서비스 구현
/// </summary>
public class LanguageService : ILanguageService
{
    /// <summary>
    /// 애플리케이션의 언어를 변경합니다.
    /// DynamicResource를 사용하는 모든 UI가 자동으로 업데이트됩니다.
    /// </summary>
    /// <param name="culture">언어 코드 (예: "ko-KR", "en-US")</param>
    public void ChangeLanguage(string culture)
    {
        try
        {
            // 새로운 언어 리소스 딕셔너리 생성
            var newDict = new ResourceDictionary
            {
                Source = new Uri($"Resources/Languages/Lang.{culture}.xaml", UriKind.Relative)
            };

            var app = Application.Current;
            if (app == null) return;

            // 기존 언어 리소스 제거
            var oldDict = app.Resources.MergedDictionaries
                .FirstOrDefault(d => d.Source?.OriginalString?.Contains("Lang.") == true);

            if (oldDict != null)
            {
                app.Resources.MergedDictionaries.Remove(oldDict);
            }

            // 새 언어 리소스 추가
            app.Resources.MergedDictionaries.Add(newDict);
        }
        catch (Exception ex)
        {
            // 언어 전환 실패 시 기본 언어(한국어)로 폴백
            System.Diagnostics.Debug.WriteLine($"언어 변경 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 시스템 언어를 감지하여 지원하는 언어 코드를 반환합니다.
    /// </summary>
    /// <returns>ko-KR (한국어) 또는 en-US (영어)</returns>
    public string GetSystemLanguage()
    {
        try
        {
            // Windows UI 언어 확인
            var systemCulture = CultureInfo.CurrentUICulture;
            var langCode = systemCulture.TwoLetterISOLanguageName.ToLower();

            // 한국어이면 ko-KR, 그 외는 en-US 반환
            return langCode == "ko" ? "ko-KR" : "en-US";
        }
        catch
        {
            // 감지 실패 시 기본값은 영어
            return "en-US";
        }
    }

    /// <summary>
    /// 현재 로드된 리소스 딕셔너리에서 텍스트를 가져옵니다.
    /// </summary>
    public string GetString(string key)
    {
        var app = Application.Current;
        if (app == null) return string.Empty;

    #if DEBUG
        return app.TryFindResource(key) as string ?? $"[{key}]";
    #else
        return app.TryFindResource(key) as string ?? string.Empty;
    #endif
    }
}
