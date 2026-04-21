using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PixConvert.Models;
using PixConvert.Services.Interfaces;

namespace PixConvert.Services;

public sealed class AppInfoService : IAppInfoService
{
    internal const string LatestReleaseApiUrl = "https://api.github.com/repos/lsj1206/PixConvert/releases/latest";

    private readonly HttpClient _httpClient;
    private readonly ILogger<AppInfoService> _logger;
    private readonly string _currentVersion;

    public string RepositoryUrl => "https://github.com/lsj1206/PixConvert";

    public string AppDataFolderPath => AppPaths.AppDataFolder;

    public AppInfoService(HttpClient httpClient, ILogger<AppInfoService> logger)
        : this(httpClient, logger, AppMetadata.DisplayVersion)
    {
    }

    internal AppInfoService(HttpClient httpClient, ILogger<AppInfoService> logger, string currentVersion)
    {
        _httpClient = httpClient;
        _logger = logger;
        _currentVersion = currentVersion;
    }

    public async Task<UpdateCheckResult> CheckLatestReleaseAsync(CancellationToken token)
    {
        try
        {
            using var response = await _httpClient.GetAsync(LatestReleaseApiUrl, token);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return CreateResult(UpdateCheckStatus.NoRelease, null, null, "Setting_App_UpdateNoRelease");

            if (!response.IsSuccessStatusCode)
                return CreateResult(UpdateCheckStatus.Failed, null, null, "Setting_App_UpdateFailed");

            await using var stream = await response.Content.ReadAsStreamAsync(token);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: token);

            string? latestVersion = ReadString(document.RootElement, "tag_name");
            string? releaseUrl = ReadString(document.RootElement, "html_url");

            if (string.IsNullOrWhiteSpace(latestVersion))
                return CreateResult(UpdateCheckStatus.Failed, null, releaseUrl, "Setting_App_UpdateFailed");

            var status = IsUpdateAvailable(_currentVersion, latestVersion)
                ? UpdateCheckStatus.UpdateAvailable
                : UpdateCheckStatus.Latest;

            string messageKey = status == UpdateCheckStatus.UpdateAvailable
                ? "Setting_App_UpdateAvailable"
                : "Setting_App_UpdateLatest";

            return CreateResult(status, latestVersion, releaseUrl, messageKey);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            return CreateResult(UpdateCheckStatus.Failed, null, null, "Setting_App_UpdateFailed");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check latest release.");
            return CreateResult(UpdateCheckStatus.Failed, null, null, "Setting_App_UpdateFailed");
        }
    }

    public IReadOnlyList<AppEngineInfo> GetEngineInfo()
    {
        return
        [
            new("SkiaSharp", GetAssemblyVersion(typeof(SkiaSharp.SKImage))),
            new("NetVips", GetAssemblyVersion(typeof(NetVips.Image)))
        ];
    }

    internal static bool IsUpdateAvailable(string currentVersion, string latestVersion)
    {
        string currentNormalized = NormalizeVersion(currentVersion);
        string latestNormalized = NormalizeVersion(latestVersion);

        if (Version.TryParse(currentNormalized, out var current) &&
            Version.TryParse(latestNormalized, out var latest))
        {
            return latest > current;
        }

        return !string.Equals(
            currentVersion.Trim(),
            latestVersion.Trim(),
            StringComparison.OrdinalIgnoreCase);
    }

    private UpdateCheckResult CreateResult(
        UpdateCheckStatus status,
        string? latestVersion,
        string? releaseUrl,
        string messageKey)
    {
        return new UpdateCheckResult(status, _currentVersion, latestVersion, releaseUrl, messageKey);
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static string NormalizeVersion(string version)
    {
        string normalized = version.Trim();
        if (normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[1..];

        int suffixIndex = normalized.IndexOfAny(['-', '+']);
        return suffixIndex >= 0 ? normalized[..suffixIndex] : normalized;
    }

    private static string GetAssemblyVersion(Type type)
    {
        var assembly = type.Assembly;
        string? version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrWhiteSpace(version))
            version = assembly.GetName().Version?.ToString();

        return string.IsNullOrWhiteSpace(version) ? "Unknown" : version.Split('+')[0];
    }

}
