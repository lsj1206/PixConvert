using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using System.Xml.Linq;
using PixConvert.Views.Dialogs;

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

    [Fact]
    public void LanguageResources_ShouldContainUserMessageKeys()
    {
        string[] expectedKeys =
        [
            "Msg_FileAddError",
            "Msg_AddFileFailed",
            "Msg_AddWithFailure",
            "Msg_OperationCompleteWithFailures",
            "Msg_OperationCompleteWithSkippedAndFailures"
        ];
        var koKeys = LoadLanguageKeys("Lang.ko-KR.xaml").ToHashSet(StringComparer.Ordinal);
        var enKeys = LoadLanguageKeys("Lang.en-US.xaml").ToHashSet(StringComparer.Ordinal);

        AssertNoMissingKeys("Lang.ko-KR.xaml", expectedKeys, koKeys);
        AssertNoMissingKeys("Lang.en-US.xaml", expectedKeys, enKeys);
    }

    [Theory]
    [InlineData("Lang.ko-KR.xaml")]
    [InlineData("Lang.en-US.xaml")]
    [InlineData("Lang.Serilog.xaml")]
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

    [Fact]
    public void ConfirmationWarningContent_ShouldLoadAsWpfControl()
    {
        Exception? exception = null;
        string? message = null;
        string? warning = null;

        var thread = new Thread(() =>
        {
            try
            {
                var content = new ConfirmationWarningContent
                {
                    Message = "Message",
                    WarningMessage = "Warning"
                };
                message = content.Message;
                warning = content.WarningMessage;
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
        Assert.Equal("Message", message);
        Assert.Equal("Warning", warning);
    }

    [Fact]
    public void LogResources_ShouldContainReferencedLogKeys()
    {
        var referencedKeys = LoadReferencedLogKeys();
        var logKeys = LoadLanguageKeys("Lang.Serilog.xaml").ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(referencedKeys);
        AssertNoMissingKeys("Lang.Serilog.xaml", referencedKeys, logKeys);
    }

    [Fact]
    public void LogResources_ShouldContainEnglishOnlyText()
    {
        var logEntries = LoadLanguageEntries("Lang.Serilog.xaml")
            .Where(pair => pair.Key.StartsWith("Log_", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(logEntries);
        foreach (var (key, value) in logEntries)
        {
            Assert.True(
                value.All(IsPrintableAscii),
                $"{key} contains non-English or mojibake text: {value}");
        }
    }

    private static IEnumerable<string> LoadLanguageKeys(string fileName)
    {
        return LoadLanguageEntries(fileName).Keys;
    }

    private static Dictionary<string, string> LoadLanguageEntries(string fileName)
    {
        XNamespace xamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";
        var path = Path.Combine(GetRepoRoot(), "src", "Resources", "Languages", fileName);
        return XDocument.Load(path)
            .Descendants()
            .Where(element => element.Attribute(xamlNamespace + "Key") is not null)
            .ToDictionary(
                element => element.Attribute(xamlNamespace + "Key")!.Value,
                element => element.Value,
                StringComparer.Ordinal);
    }

    private static HashSet<string> LoadConvertSettingDialogSettingTipKeys()
    {
        var path = Path.Combine(GetRepoRoot(), "src", "Views", "Dialogs", "ConvertSettingDialog.xaml");
        var xaml = File.ReadAllText(path);
        return Regex.Matches(xaml, @"DynamicResource\s+(SettingTip_[A-Za-z0-9_]+)")
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static HashSet<string> LoadReferencedLogKeys()
    {
        var srcPath = Path.Combine(GetRepoRoot(), "src");
        return Directory.EnumerateFiles(srcPath, "*.cs", SearchOption.AllDirectories)
            .SelectMany(path => Regex.Matches(File.ReadAllText(path), @"\bLog_[A-Za-z0-9_]+\b"))
            .Select(match => match.Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static bool IsPrintableAscii(char value)
    {
        return value is >= ' ' and <= '~' or '\r' or '\n' or '\t';
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
