using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PixConvert.Models;

namespace PixConvert.ViewModels;

/// <summary>
/// ConversionViewModel에서 사용하는 사이드바 표시용 요약 문자열 생성 로직을 분리한 헬퍼입니다.
/// </summary>
internal static class ConversionSummaryBuilder
{
    /// <summary>
    /// 현재 일반 이미지와 애니메이션 입력 구성에 맞는 대상 포맷 요약 문자열을 생성합니다.
    /// </summary>
    public static string BuildTargetFormatSummary(ConvertSettings settings, IReadOnlyList<FileItem> activeFiles)
    {
        bool hasStandard = activeFiles.Any(file => !file.IsAnimation);
        bool hasAnimation = activeFiles.Any(file => file.IsAnimation);

        if (hasStandard && hasAnimation)
            return $"{settings.StandardTargetFormat} / {settings.AnimationTargetFormat}";

        if (hasAnimation)
            return settings.AnimationTargetFormat ?? string.Empty;

        return settings.StandardTargetFormat;
    }

    /// <summary>
    /// 변환 파일이 어떻게 저장될지 보여주는 사이드바 문자열을 생성합니다.
    /// </summary>
    public static string BuildSaveMethodSummary(ConvertSettings settings, Func<string, string> getString)
    {
        return settings.FolderMethod == SaveFolderMethod.CreateFolder
            ? settings.OutputSubFolderName
            : getString($"Setting_SaveMethod_{settings.FolderMethod}");
    }

    /// <summary>
    /// 출력 위치에 표시할 축약 문자열과 툴팁 문자열을 생성합니다.
    /// </summary>
    public static SaveLocationSummary BuildSaveLocationSummary(ConvertSettings settings, Func<string, string> getString)
    {
        if (settings.SaveLocation == SaveLocationType.SameAsOriginal)
        {
            string sameText = getString("Setting_SaveLocation_Same");
            string displayText = sameText.StartsWith("...", StringComparison.Ordinal)
                ? sameText
                : $"...{sameText}";
            return new SaveLocationSummary(displayText, string.Empty);
        }

        string targetPath = settings.CustomOutputPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string folderName = Path.GetFileName(targetPath);
        if (string.IsNullOrEmpty(folderName))
            folderName = settings.CustomOutputPath;

        return new SaveLocationSummary($"...{folderName}", settings.CustomOutputPath);
    }

    /// <summary>
    /// 일반 이미지가 있을 때 비애니메이션 출력 포맷용 옵션 요약 문자열을 생성합니다.
    /// </summary>
    public static string BuildStandardOptionsSummary(
        ConvertSettings settings,
        IReadOnlyList<FileItem> activeFiles,
        Func<string, string> getString)
    {
        if (!activeFiles.Any(file => !file.IsAnimation))
            return string.Empty;

        var parts = new List<string>();
        string format = settings.StandardTargetFormat.ToUpperInvariant();

        switch (format)
        {
            case "JPEG":
                parts.Add($"{getString("Setting_Quality")} {settings.StandardQuality}");
                parts.Add($"{getString("Setting_ChromaSubsampling")} {ResolveJpegChromaSummary(settings, activeFiles, getString)}");
                parts.Add($"{getString("Converting_BgColor")} {settings.StandardBackgroundColor}");
                break;
            case "PNG":
                parts.Add($"{getString("Setting_CompressionLevel")} {settings.StandardPngCompressionLevel}");
                break;
            case "WEBP":
                parts.Add(settings.StandardLossless
                    ? getString("Setting_Lossless")
                    : $"{getString("Setting_Quality")} {settings.StandardQuality}");
                break;
            case "AVIF":
                if (settings.StandardLossless)
                {
                    parts.Add(getString("Setting_Lossless"));
                }
                else
                {
                    parts.Add($"{getString("Setting_Quality")} {settings.StandardQuality}");
                    parts.Add($"{getString("Setting_ChromaSubsampling")} {ResolveAvifChromaSummary(settings.StandardAvifChromaSubsampling, getString)}");
                }

                parts.Add($"{getString("Setting_EncodingEffort")} {settings.StandardAvifEncodingEffort}");
                parts.Add($"{getString("Setting_BitDepth")} {ResolveAvifBitDepthSummary(settings.StandardAvifBitDepth, getString)}");
                break;
            case "BMP":
                parts.Add($"{getString("Converting_BgColor")} {settings.StandardBackgroundColor}");
                break;
        }

        return string.Join(Environment.NewLine, parts);
    }

    /// <summary>
    /// 애니메이션 이미지가 있을 때 애니메이션 출력 포맷용 옵션 요약 문자열을 생성합니다.
    /// </summary>
    public static string BuildAnimationOptionsSummary(
        ConvertSettings settings,
        IReadOnlyList<FileItem> activeFiles,
        Func<string, string> getString)
    {
        if (!activeFiles.Any(file => file.IsAnimation) || string.IsNullOrWhiteSpace(settings.AnimationTargetFormat))
            return string.Empty;

        var parts = new List<string>();
        string format = settings.AnimationTargetFormat.ToUpperInvariant();

        switch (format)
        {
            case "WEBP":
                if (settings.AnimationLossless)
                {
                    parts.Add(getString("Setting_Lossless"));
                    parts.Add($"{getString("Setting_EncodingEffort")} {settings.AnimationWebpEncodingEffort}");
                    parts.Add($"{getString("Setting_PreserveTransparentPixels")} {ResolveBooleanSummary(settings.AnimationWebpPreserveTransparentPixels, getString)}");
                }
                else
                {
                    parts.Add($"{getString("Setting_Quality")} {settings.AnimationQuality}");
                    parts.Add($"{getString("Setting_EncodingEffort")} {settings.AnimationWebpEncodingEffort}");
                    parts.Add($"{getString("Setting_WebpPreset")} {getString($"Setting_WebpPreset_{settings.AnimationWebpPreset}")}");
                }
                break;
            case "GIF":
                parts.Add($"{getString("Setting_PalettePreset")} {getString($"Setting_GifPalette_{settings.AnimationGifPalettePreset}")}");
                parts.Add($"{getString("Setting_EncodingEffort")} {settings.AnimationGifEncodingEffort}");
                parts.Add($"{getString("Setting_InterframeMaxError")} {FormatErrorValue(settings.AnimationGifInterframeMaxError)}");
                parts.Add($"{getString("Setting_InterpaletteMaxError")} {FormatErrorValue(settings.AnimationGifInterpaletteMaxError)}");
                break;
        }

        return string.Join(Environment.NewLine, parts);
    }

    /// <summary>
    /// JPEG 4:2:2 모드의 AVIF 입력 예외를 포함해 크로마 서브샘플링 표시 문자열을 결정합니다.
    /// </summary>
    private static string ResolveJpegChromaSummary(
        ConvertSettings settings,
        IReadOnlyList<FileItem> activeFiles,
        Func<string, string> getString)
    {
        bool hasAvifInput = activeFiles.Any(file =>
            !file.IsAnimation &&
            string.Equals(file.FileSignature, "AVIF", StringComparison.OrdinalIgnoreCase));

        if (settings.StandardJpegChromaSubsampling == JpegChromaSubsamplingMode.Chroma422 && hasAvifInput)
            return getString("Converting_Jpeg422AvifAuto");

        return settings.StandardJpegChromaSubsampling switch
        {
            JpegChromaSubsamplingMode.Chroma420 => getString("Setting_Subsampling_420"),
            JpegChromaSubsamplingMode.Chroma422 => getString("Setting_Subsampling_422"),
            _ => getString("Setting_Subsampling_444")
        };
    }

    /// <summary>
    /// 현재 AVIF 크로마 서브샘플링 모드에 맞는 지역화 문자열을 반환합니다.
    /// </summary>
    private static string ResolveAvifChromaSummary(AvifChromaSubsamplingMode mode, Func<string, string> getString)
    {
        return mode switch
        {
            AvifChromaSubsamplingMode.On => getString("Setting_Subsampling_On"),
            AvifChromaSubsamplingMode.Off => getString("Setting_Subsampling_Off"),
            _ => getString("Setting_Subsampling_Auto")
        };
    }

    /// <summary>
    /// 현재 AVIF 비트 깊이 모드에 맞는 지역화 문자열을 반환합니다.
    /// </summary>
    private static string ResolveAvifBitDepthSummary(AvifBitDepthMode mode, Func<string, string> getString)
    {
        return mode switch
        {
            AvifBitDepthMode.Bit8 => getString("Setting_BitDepth_8"),
            AvifBitDepthMode.Bit10 => getString("Setting_BitDepth_10"),
            AvifBitDepthMode.Bit12 => getString("Setting_BitDepth_12"),
            _ => getString("Setting_BitDepth_Auto")
        };
    }

    /// <summary>
    /// 불리언 옵션 값을 요약 문자열에 쓰이는 켜짐/꺼짐 텍스트로 변환합니다.
    /// </summary>
    private static string ResolveBooleanSummary(bool value, Func<string, string> getString) =>
        getString(value ? "Setting_Subsampling_On" : "Setting_Subsampling_Off");

    /// <summary>
    /// GIF 오차 임계값을 UI와 같은 고정 소수점 형식으로 변환합니다.
    /// </summary>
    private static string FormatErrorValue(double value) =>
        value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>
/// 출력 경로는 짧은 표시 문자열과 전체 툴팁 문자열을 따로 관리합니다.
/// </summary>
internal readonly record struct SaveLocationSummary(string DisplayText, string TooltipText);
