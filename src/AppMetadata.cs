using System.Reflection;

namespace PixConvert;

/// <summary>
/// 빌드 메타데이터에서 앱 버전 표시 문자열을 읽어오는 정적 진입점입니다.
/// </summary>
public static class AppMetadata
{
    /// <summary>
    /// UI와 서비스에서 공통으로 사용할 표시용 버전 문자열입니다.
    /// </summary>
    public static string DisplayVersion => ResolveDisplayVersion();

    private static string ResolveDisplayVersion()
    {
        var assembly = typeof(AppMetadata).Assembly;
        string? version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (string.IsNullOrWhiteSpace(version))
        {
            version = assembly.GetName().Version?.ToString();
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            return "Unknown";
        }

        return version.Split('+')[0];
    }
}
