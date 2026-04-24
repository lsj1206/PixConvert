using PixConvert.Models;

namespace PixConvert.Services.Interfaces;

public interface IAppInfoService
{
    string RepositoryUrl { get; }

    string DataFolderPath { get; }

    Task<UpdateCheckResult> CheckLatestReleaseAsync(CancellationToken token);

    IReadOnlyList<AppEngineInfo> GetEngineInfo();
}
