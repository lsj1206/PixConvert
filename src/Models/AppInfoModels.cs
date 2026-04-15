namespace PixConvert.Models;

public enum UpdateCheckStatus
{
    Unknown,
    Checking,
    Latest,
    UpdateAvailable,
    NoRelease,
    Failed
}

public sealed record UpdateCheckResult(
    UpdateCheckStatus Status,
    string CurrentVersion,
    string? LatestVersion,
    string? ReleaseUrl,
    string MessageKey);

public sealed record AppEngineInfo(string Name, string Version);
