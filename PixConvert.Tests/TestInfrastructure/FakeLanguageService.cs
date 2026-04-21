using System;
using PixConvert.Services;

namespace PixConvert.Tests;

internal sealed class FakeLanguageService : ILanguageService
{
    public string GetString(string key) => key;

    public void ChangeLanguage(string culture) { }

    public string GetSystemLanguage() => "ko-KR";

    public string GetCurrentLanguage() => "ko-KR";

    public event Action LanguageChanged = delegate { };
}
