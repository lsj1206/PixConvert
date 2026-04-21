using System.Threading;
using System.Threading.Tasks;
using PixConvert.Models;
using Xunit;

namespace PixConvert.Tests;

public class StaticConversionMatrixTests : IClassFixture<ConversionIntegrationFixture>
{
    private readonly ConversionIntegrationFixture _fixture;

    public StaticConversionMatrixTests(ConversionIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    public static TheoryData<string, string> StaticConversionCases() =>
        new()
        {
            { "JPEG", "JPEG" },
            { "JPEG", "PNG" },
            { "JPEG", "BMP" },
            { "JPEG", "WEBP" },
            { "JPEG", "AVIF" },
            { "PNG", "JPEG" },
            { "PNG", "PNG" },
            { "PNG", "BMP" },
            { "PNG", "WEBP" },
            { "PNG", "AVIF" },
            { "BMP", "JPEG" },
            { "BMP", "PNG" },
            { "BMP", "BMP" },
            { "BMP", "WEBP" },
            { "BMP", "AVIF" },
            { "WEBP", "JPEG" },
            { "WEBP", "PNG" },
            { "WEBP", "BMP" },
            { "WEBP", "WEBP" },
            { "WEBP", "AVIF" },
            { "AVIF", "JPEG" },
            { "AVIF", "PNG" },
            { "AVIF", "BMP" },
            { "AVIF", "WEBP" },
            { "AVIF", "AVIF" }
        };

    [Theory]
    [MemberData(nameof(StaticConversionCases))]
    public async Task StaticConversionMatrix_ShouldConvertEverySupportedCombination(string sourceFormat, string targetFormat)
    {
        string scenarioName = $"static_{sourceFormat}_{targetFormat}";
        FileItem file = await _fixture.CreateStaticInputAsync(sourceFormat, scenarioName);
        var settings = ConversionIntegrationFixture.CreateStaticSettings(targetFormat);

        var provider = _fixture.GetProvider(file, settings);
        Assert.Equal(ConversionIntegrationFixture.ExpectedStaticProviderName(sourceFormat, targetFormat), provider.Name);

        var result = await provider.ConvertAsync(file, settings, new ConversionSession(), CancellationToken.None);

        ConversionIntegrationFixture.AssertSuccessfulOutput(result, targetFormat);
        ConversionIntegrationFixture.AssertImageCanReopen(result.OutputPath!);
    }
}
