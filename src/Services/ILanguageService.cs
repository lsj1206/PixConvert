using System.Globalization;

namespace PixConvert.Services;

/// <summary>
/// 애플리케이션 언어 관리 서비스
/// </summary>
public interface ILanguageService
{
    /// <summary>
    /// 애플리케이션의 언어를 변경합니다.
    /// </summary>
    /// <param name="culture">언어 코드 (예: "ko-KR", "en-US")</param>
    void ChangeLanguage(string culture);

    /// <summary>
    /// 시스템 언어를 감지하여 지원하는 언어 코드를 반환합니다.
    /// </summary>
    /// <returns>언어 코드 (ko-KR 또는 en-US)</returns>
    string GetSystemLanguage();

    /// <summary>
    /// 리소스 키를 사용하여 번역된 텍스트를 가져옵니다.
    /// </summary>
    string GetString(string key);
}
