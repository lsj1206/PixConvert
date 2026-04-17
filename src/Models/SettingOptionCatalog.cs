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
    BackgroundColor,
    JpegChromaSubsampling,
    PngCompression,
    AvifChromaSubsampling,
    AvifEncodingEffort,
    AvifBitDepth,
    GifPalettePreset,
    GifEncodingEffort,
    GifInterframeMaxError,
    GifInterpaletteMaxError,
    WebpEncodingEffort,
    WebpPreset,
    WebpPreserveTransparentPixels
}

public sealed record SettingOptionDefinition(
    SettingOptionSection Section,
    SettingOptionKey Key,
    string[] SupportedTargetFormats);

public static class SettingOptionCatalog
{
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
            SettingOptionSection.Standard,
            SettingOptionKey.JpegChromaSubsampling,
            new[] { "JPEG" }),
        new(
            SettingOptionSection.Standard,
            SettingOptionKey.PngCompression,
            new[] { "PNG" }),
        new(
            SettingOptionSection.Standard,
            SettingOptionKey.AvifChromaSubsampling,
            new[] { "AVIF" }),
        new(
            SettingOptionSection.Standard,
            SettingOptionKey.AvifEncodingEffort,
            new[] { "AVIF" }),
        new(
            SettingOptionSection.Standard,
            SettingOptionKey.AvifBitDepth,
            new[] { "AVIF" }),
        new(
            SettingOptionSection.Animation,
            SettingOptionKey.Lossless,
            new[] { "WEBP" }),
        new(
            SettingOptionSection.Animation,
            SettingOptionKey.Quality,
            new[] { "WEBP" }),
        new(
            SettingOptionSection.Animation,
            SettingOptionKey.GifPalettePreset,
            new[] { "GIF" }),
        new(
            SettingOptionSection.Animation,
            SettingOptionKey.GifEncodingEffort,
            new[] { "GIF" }),
        new(
            SettingOptionSection.Animation,
            SettingOptionKey.GifInterframeMaxError,
            new[] { "GIF" }),
        new(
            SettingOptionSection.Animation,
            SettingOptionKey.GifInterpaletteMaxError,
            new[] { "GIF" }),
        new(
            SettingOptionSection.Animation,
            SettingOptionKey.WebpEncodingEffort,
            new[] { "WEBP" }),
        new(
            SettingOptionSection.Animation,
            SettingOptionKey.WebpPreset,
            new[] { "WEBP" }),
        new(
            SettingOptionSection.Animation,
            SettingOptionKey.WebpPreserveTransparentPixels,
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
