using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using System.Xml.Linq;

namespace PixConvert.Tests;

public class LanguageResourceTests
{
    [Fact]
    public void LanguageResources_ShouldContainSameSettingTipKeys()
    {
        var koKeys = LoadLanguageKeys("Lang.ko-KR.xaml")
            .Where(key => key.StartsWith("SettingTip_", StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);
        var enKeys = LoadLanguageKeys("Lang.en-US.xaml")
            .Where(key => key.StartsWith("SettingTip_", StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(koKeys);
        AssertNoMissingKeys("Lang.en-US.xaml", koKeys, enKeys);
        AssertNoMissingKeys("Lang.ko-KR.xaml", enKeys, koKeys);
    }

    [Fact]
    public void ConvertSettingDialog_ShouldReferenceExistingSettingTipKeys()
    {
        var referencedKeys = LoadConvertSettingDialogSettingTipKeys();
        var koKeys = LoadLanguageKeys("Lang.ko-KR.xaml").ToHashSet(StringComparer.Ordinal);
        var enKeys = LoadLanguageKeys("Lang.en-US.xaml").ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(referencedKeys);
        AssertNoMissingKeys("Lang.ko-KR.xaml", referencedKeys, koKeys);
        AssertNoMissingKeys("Lang.en-US.xaml", referencedKeys, enKeys);
    }

    [Fact]
    public void LanguageResources_ShouldContainAppSettingInfoKeys()
    {
        string[] expectedKeys =
        [
            "Setting_App_GitHub",
            "Setting_App_Engine",
            "Setting_App_DataFolder",
            "Setting_App_CheckUpdate",
            "Setting_App_OpenGitHub",
            "Setting_App_OpenDataFolder",
            "Setting_App_UpdateChecking",
            "Setting_App_UpdateLatest",
            "Setting_App_UpdateAvailable",
            "Setting_App_UpdateNoRelease",
            "Setting_App_UpdateFailed"
        ];
        var koKeys = LoadLanguageKeys("Lang.ko-KR.xaml").ToHashSet(StringComparer.Ordinal);
        var enKeys = LoadLanguageKeys("Lang.en-US.xaml").ToHashSet(StringComparer.Ordinal);

        AssertNoMissingKeys("Lang.ko-KR.xaml", expectedKeys, koKeys);
        AssertNoMissingKeys("Lang.en-US.xaml", expectedKeys, enKeys);
    }

    [Theory]
    [InlineData("Lang.ko-KR.xaml")]
    [InlineData("Lang.en-US.xaml")]
    public void LanguageResources_ShouldLoadAsWpfResourceDictionary(string fileName)
    {
        Exception? exception = null;

        var thread = new Thread(() =>
        {
            try
            {
                var path = Path.Combine(GetRepoRoot(), "src", "Resources", "Languages", fileName);
                using var stream = File.OpenRead(path);
                Assert.IsType<ResourceDictionary>(XamlReader.Load(stream));
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(exception);
    }

    private static IEnumerable<string> LoadLanguageKeys(string fileName)
    {
        XNamespace xamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";
        var path = Path.Combine(GetRepoRoot(), "src", "Resources", "Languages", fileName);
        return XDocument.Load(path)
            .Descendants()
            .Attributes(xamlNamespace + "Key")
            .Select(attribute => attribute.Value)
            .ToArray();
    }

    private static HashSet<string> LoadConvertSettingDialogSettingTipKeys()
    {
        var path = Path.Combine(GetRepoRoot(), "src", "Views", "Dialogs", "ConvertSettingDialog.xaml");
        var xaml = File.ReadAllText(path);
        return Regex.Matches(xaml, @"DynamicResource\s+(SettingTip_[A-Za-z0-9_]+)")
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static void AssertNoMissingKeys(
        string targetName,
        IEnumerable<string> expectedKeys,
        ISet<string> actualKeys)
    {
        var missingKeys = expectedKeys
            .Where(key => !actualKeys.Contains(key))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            missingKeys.Length == 0,
            $"{targetName} is missing resource keys: {string.Join(", ", missingKeys)}");
    }

    private static string GetRepoRoot([CallerFilePath] string sourcePath = "")
    {
        return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, ".."));
    }
}
