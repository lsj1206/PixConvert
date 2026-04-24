using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Logging.Abstractions;
using PixConvert.Models;
using PixConvert.Services;
using Xunit;

namespace PixConvert.Tests;

public class AppInfoServiceTests
{
    [Fact]
    public void Properties_ShouldUseMetadataBackedValues()
    {
        var service = CreateService(new HttpResponseMessage(HttpStatusCode.OK), "v1.0.0");

        Assert.Equal(AppMetadata.RepositoryUrl, service.RepositoryUrl);
        Assert.Equal(AppPaths.DataFolder, service.DataFolderPath);
    }

    [Fact]
    public async Task CheckLatestReleaseAsync_WhenLatestTagIsNewer_ShouldReturnUpdateAvailable()
    {
        var service = CreateService(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"tag_name":"v1.2.0","html_url":"https://example.com/release"}""")
            },
            "v1.0.0");

        var result = await service.CheckLatestReleaseAsync(CancellationToken.None);

        Assert.Equal(UpdateCheckStatus.UpdateAvailable, result.Status);
        Assert.Equal("v1.2.0", result.LatestVersion);
        Assert.Equal("https://example.com/release", result.ReleaseUrl);
        Assert.Equal("Setting_App_UpdateAvailable", result.MessageKey);
    }

    [Fact]
    public async Task CheckLatestReleaseAsync_WhenLatestTagIsSame_ShouldReturnLatest()
    {
        var service = CreateService(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"tag_name":"v1.0.0","html_url":"https://example.com/release"}""")
            },
            "v1.0.0");

        var result = await service.CheckLatestReleaseAsync(CancellationToken.None);

        Assert.Equal(UpdateCheckStatus.Latest, result.Status);
        Assert.Equal("Setting_App_UpdateLatest", result.MessageKey);
    }

    [Fact]
    public async Task CheckLatestReleaseAsync_WhenReleaseDoesNotExist_ShouldReturnNoRelease()
    {
        var service = CreateService(new HttpResponseMessage(HttpStatusCode.NotFound), "v1.0.0");

        var result = await service.CheckLatestReleaseAsync(CancellationToken.None);

        Assert.Equal(UpdateCheckStatus.NoRelease, result.Status);
        Assert.Equal("Setting_App_UpdateNoRelease", result.MessageKey);
    }

    [Fact]
    public async Task CheckLatestReleaseAsync_WhenRequestFails_ShouldReturnFailed()
    {
        var service = CreateService(_ => throw new HttpRequestException("network"), "v1.0.0");

        var result = await service.CheckLatestReleaseAsync(CancellationToken.None);

        Assert.Equal(UpdateCheckStatus.Failed, result.Status);
        Assert.Equal("Setting_App_UpdateFailed", result.MessageKey);
    }

    [Fact]
    public async Task CheckLatestReleaseAsync_WhenCurrentVersionIsNonSemver_ShouldUseStringFallback()
    {
        var service = CreateService(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"tag_name":"v1.0.0","html_url":"https://example.com/release"}""")
            },
            "v.alpha");

        var result = await service.CheckLatestReleaseAsync(CancellationToken.None);

        Assert.Equal(UpdateCheckStatus.UpdateAvailable, result.Status);
    }

    [Fact]
    public async Task CheckLatestReleaseAsync_ShouldRequestConfiguredMetadataUrl()
    {
        Uri? requestedUri = null;
        var httpClient = new HttpClient(new DelegateHandler((request, _) =>
        {
            requestedUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }));
        var service = new AppInfoService(httpClient, NullLogger<AppInfoService>.Instance, "v1.0.0");

        await service.CheckLatestReleaseAsync(CancellationToken.None);

        Assert.Equal(AppMetadata.LatestReleaseApiUrl, requestedUri?.ToString());
    }

    private static AppInfoService CreateService(HttpResponseMessage response, string currentVersion)
    {
        return CreateService(_ => response, currentVersion);
    }

    private static AppInfoService CreateService(Func<HttpRequestMessage, HttpResponseMessage> send, string currentVersion)
    {
        var httpClient = new HttpClient(new DelegateHandler((request, _) => Task.FromResult(send(request))));
        return new AppInfoService(httpClient, NullLogger<AppInfoService>.Instance, currentVersion);
    }

    private sealed class DelegateHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _send;

        public DelegateHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        {
            _send = send;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _send(request, cancellationToken);
        }
    }
}
