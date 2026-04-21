using System.Threading;
using System.Threading.Tasks;
using PixConvert.Models;
using Xunit;

namespace PixConvert.Tests;

public class AnimatedConversionMatrixTests : IClassFixture<ConversionIntegrationFixture>
{
    private readonly ConversionIntegrationFixture _fixture;

    public AnimatedConversionMatrixTests(ConversionIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    public static TheoryData<string, string> AnimatedConversionCases() =>
        new()
        {
            { "GIF", "GIF" },
            { "GIF", "WEBP" },
            { "WEBP", "GIF" },
            { "WEBP", "WEBP" }
        };

    [Theory]
    [MemberData(nameof(AnimatedConversionCases))]
    public async Task AnimatedConversionMatrix_ShouldConvertEverySupportedCombinationAndKeepMultipleFrames(
        string sourceFormat,
        string targetFormat)
    {
        string scenarioName = $"animated_{sourceFormat}_{targetFormat}";
        FileItem file = _fixture.CreateAnimatedInput(sourceFormat, scenarioName);
        var settings = ConversionIntegrationFixture.CreateAnimationSettings(targetFormat);

        var provider = _fixture.GetProvider(file, settings);
        Assert.Equal("NetVips", provider.Name);

        var result = await provider.ConvertAsync(file, settings, new ConversionSession(), CancellationToken.None);

        ConversionIntegrationFixture.AssertSuccessfulOutput(result, targetFormat);
        ConversionIntegrationFixture.AssertImageCanReopen(result.OutputPath!);
        Assert.True(
            ConversionIntegrationFixture.GetLoadedFrameCount(result.OutputPath!) >= 2,
            $"{sourceFormat} -> {targetFormat} collapsed to a single frame.");
    }
}
