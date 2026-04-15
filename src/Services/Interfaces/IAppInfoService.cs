using PixConvert.Models;

namespace PixConvert.Services.Interfaces;

public interface IAppInfoService
{
    string RepositoryUrl { get; }

    string AppDataFolderPath { get; }

    Task<UpdateCheckResult> CheckLatestReleaseAsync(CancellationToken token);

    IReadOnlyList<AppEngineInfo> GetEngineInfo();
}
