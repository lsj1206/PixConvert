using System.IO;
using PixConvert.Models;

namespace PixConvert.Tests;

internal static class ProviderTestAssertions
{
    public static void AssertSuccessResult(ConversionResult result, string expectedPath)
    {
        Assert.Equal(FileConvertStatus.Success, result.Status);
        Assert.Equal(expectedPath, result.OutputPath);
        Assert.True(result.OutputSize > 0);
        Assert.True(File.Exists(result.OutputPath), $"Missing output file: {result.OutputPath}");
    }

    public static void AssertProviderDidNotMutateFileItem(FileItem file)
    {
        Assert.Equal(FileConvertStatus.Pending, file.Status);
        Assert.Equal(0, file.Progress);
        Assert.Null(file.OutputPath);
        Assert.Equal(0, file.OutputSize);
    }
}
