using System.Reflection;
using System.Linq;

namespace PixConvert;

/// <summary>
/// 빌드 메타데이터와 앱 내부 기본값을 한 곳에서 제공하는 정적 진입점입니다.
/// </summary>
public static class AppMetadata
{
    internal const string RepositoryUrlKey = "RepositoryUrl";
    internal const string LatestReleaseApiUrlKey = "LatestReleaseApiUrl";

    /// <summary>UI와 서비스에서 공통으로 사용할 표시용 버전 문자열입니다.</summary>
    public static string DisplayVersion => ResolveDisplayVersion();

    /// <summary>앱에서 사용하는 저장소 주소입니다.</summary>
    public static string RepositoryUrl => GetMetadataValue(RepositoryUrlKey, "https://github.com/lsj1206/PixConvert");

    /// <summary>최신 릴리스를 조회할 GitHub API 주소입니다.</summary>
    public static string LatestReleaseApiUrl => GetMetadataValue(
        LatestReleaseApiUrlKey,
        "https://api.github.com/repos/lsj1206/PixConvert/releases/latest");

    /// <summary>앱 실행 폴더 아래에 생성할 로그 폴더 이름입니다.</summary>
    public static string LogsFolderName => "logs";

    /// <summary>설정 파일 이름입니다.</summary>
    public static string SettingsFileName => "settings.json";

    /// <summary>프리셋 파일 이름입니다.</summary>
    public static string PresetsFileName => "presets.json";

    /// <summary>로그 파일 이름 앞에 붙일 접두사입니다.</summary>
    public static string LogFilePrefix => "pixconvert_log_";

    /// <summary>HTTP 요청에 사용할 기본 User-Agent 값입니다.</summary>
    public static string HttpUserAgent => "PixConvert";

    /// <summary>별도 지정이 없을 때 사용할 기본 출력 하위 폴더 이름입니다.</summary>
    public static string DefaultOutputSubFolderName => "PixConvert";

    private static string ResolveDisplayVersion() => ResolveDisplayVersion(typeof(AppMetadata).Assembly);

    internal static string ResolveDisplayVersion(Assembly assembly)
    {
        string? version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return ResolveDisplayVersion(version, assembly.GetName().Version?.ToString());
    }

    internal static string ResolveDisplayVersion(string? informationalVersion, string? assemblyVersion)
    {
        string? version = informationalVersion;

        if (string.IsNullOrWhiteSpace(version))
        {
            version = assemblyVersion;
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            return "Unknown";
        }

        return version.Split('+')[0];
    }

    internal static string GetMetadataValue(string key, string fallback) =>
        GetMetadataValue(typeof(AppMetadata).Assembly, key, fallback);

    internal static string GetMetadataValue(Assembly assembly, string key, string fallback)
    {
        string? value = assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == key)
            ?.Value;

        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}
