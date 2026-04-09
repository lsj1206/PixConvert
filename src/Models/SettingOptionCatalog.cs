using System;
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

public sealed record SettingOptionDefinition(
    SettingOptionSection Section,
    SettingOptionKey Key,
    string[] SupportedTargetFormats);

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
            new[] { "WEBP", "AVIF" }),
        new(
            SettingOptionSection.Standard,
            SettingOptionKey.Quality,
            new[] { "JPEG", "WEBP", "AVIF" }),
        new(
            SettingOptionSection.Standard,
            SettingOptionKey.BackgroundColor,
            new[] { "JPEG", "BMP" }),
        new(
            SettingOptionSection.Animation,
            SettingOptionKey.Lossless,
            new[] { "WEBP" }),
        new(
            SettingOptionSection.Animation,
            SettingOptionKey.Quality,
            new[] { "WEBP" })
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
