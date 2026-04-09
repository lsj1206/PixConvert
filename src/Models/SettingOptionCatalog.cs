using System;
using System.Collections.Generic;
using System.Linq;

namespace PixConvert.Models;

public enum SettingOptionSection
{
    Standard,
    Animation
}

public enum SettingOptionKey
{
    Lossless,
    Quality,
    BackgroundColor
}

public enum SettingOptionEngineSupport
{
    Both,
    SkiaSharp,
    NetVips
}

public sealed record SettingOptionDefinition(
    SettingOptionSection Section,
    SettingOptionKey Key,
    string LabelResourceKey,
    IReadOnlyCollection<string> SupportedTargetFormats,
    SettingOptionEngineSupport SupportedEngines,
    object DefaultValue,
    bool ShowAniTag = false);

public static class SettingOptionCatalog
{
    // TODO:
    // - 한쪽 엔진만 지원하는 옵션은 사용자 설정으로 노출하지 않고 내부 자동세팅 후보로 유지합니다.
    // - 후속 후보: PNG compression, JPEG subsampling, AVIF effort, GIF dither, GIF reuse, WebP near-lossless
    public static readonly IReadOnlyList<SettingOptionDefinition> All =
    [
        new(
            SettingOptionSection.Standard,
            SettingOptionKey.Lossless,
            "Setting_Lossless",
            new[] { "WEBP", "AVIF" },
            SettingOptionEngineSupport.Both,
            false),
        new(
            SettingOptionSection.Standard,
            SettingOptionKey.Quality,
            "Setting_Quality",
            new[] { "JPEG", "WEBP", "AVIF" },
            SettingOptionEngineSupport.Both,
            85),
        new(
            SettingOptionSection.Standard,
            SettingOptionKey.BackgroundColor,
            "Setting_BackgroundColor",
            new[] { "JPEG", "BMP" },
            SettingOptionEngineSupport.Both,
            "#FFFFFF"),
        new(
            SettingOptionSection.Animation,
            SettingOptionKey.Lossless,
            "Setting_Lossless",
            new[] { "WEBP" },
            SettingOptionEngineSupport.NetVips,
            false),
        new(
            SettingOptionSection.Animation,
            SettingOptionKey.Quality,
            "Setting_Quality",
            new[] { "WEBP" },
            SettingOptionEngineSupport.NetVips,
            85)
    ];

    public static bool Supports(SettingOptionSection section, SettingOptionKey key, string? targetFormat)
    {
        if (string.IsNullOrWhiteSpace(targetFormat))
            return false;

        return All.Any(option =>
            option.Section == section &&
            option.Key == key &&
            option.SupportedTargetFormats.Contains(targetFormat, StringComparer.OrdinalIgnoreCase));
    }
}
