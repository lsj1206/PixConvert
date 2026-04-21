using System;
using System.IO;
using Microsoft.Extensions.Logging;
using PixConvert.Models;
using PixConvert.Services.Interfaces;

namespace PixConvert.Services.Providers;

/// <summary>
/// 공급자 구현들이 공통으로 사용하는 출력 경로 준비와 설정 해석 로직을 제공합니다.
/// </summary>
internal static class ProviderConversionHelper
{
    /// <summary>
    /// 저장 경로 계산, 덮어쓰기 정책 적용, 충돌 로그, 출력 폴더 생성을 한 번에 처리합니다.
    /// </summary>
    public static string? PrepareOutputPath(
        FileItem file,
        ConvertSettings settings,
        ConversionSession session,
        ILogger logger,
        ILanguageService languageService)
    {
        string basePath = OutputPathResolver.Resolve(file, settings);
        var (outputPath, isCollision) = OutputPathResolver.ApplyOverwritePolicy(basePath, settings.OverwritePolicy, session, file.Path);

        if (isCollision && outputPath is not null)
        {
            logger.LogWarning(languageService.GetString("Log_Conversion_PathCollision"), outputPath);
        }

        if (outputPath is null)
            return null;

        string outputDir = Path.GetDirectoryName(outputPath)!;
        if (!string.IsNullOrEmpty(outputDir))
            Directory.CreateDirectory(outputDir);

        return outputPath;
    }

    /// <summary>
    /// 입력 파일 종류에 따라 실제 변환 대상 포맷을 결정합니다.
    /// </summary>
    public static string ResolveTargetFormat(FileItem file, ConvertSettings settings)
    {
        return file.IsAnimation
            ? settings.AnimationTargetFormat ?? throw new InvalidOperationException("AnimationTargetFormat is required for animation output.")
            : settings.StandardTargetFormat;
    }

    /// <summary>
    /// 생성된 출력 파일의 바이트 크기를 반환합니다.
    /// </summary>
    public static long GetOutputSize(string outputPath) =>
        File.Exists(outputPath) ? new FileInfo(outputPath).Length : 0;

    /// <summary>
    /// 현재 변환 대상에 맞는 품질 값을 반환합니다.
    /// </summary>
    public static int GetQuality(ConvertSettings settings, bool isAnimation) =>
        isAnimation ? settings.AnimationQuality : settings.StandardQuality;

    /// <summary>
    /// 현재 변환 대상에 맞는 무손실 여부를 반환합니다.
    /// </summary>
    public static bool GetLossless(ConvertSettings settings, bool isAnimation) =>
        isAnimation ? settings.AnimationLossless : settings.StandardLossless;

    /// <summary>
    /// 배경색 설정을 공급자 공통 색상 값으로 변환합니다.
    /// </summary>
    public static ProviderBackgroundColor GetBackgroundColor(ConvertSettings settings)
    {
        string backgroundColor = settings.StandardBackgroundColor ?? "#FFFFFF";
        return ParseBackgroundColor(backgroundColor);
    }

    private static ProviderBackgroundColor ParseBackgroundColor(string hex)
    {
        try
        {
            string clean = hex.TrimStart('#');
            return clean.Length switch
            {
                6 => new ProviderBackgroundColor(
                    255,
                    Convert.ToByte(clean[0..2], 16),
                    Convert.ToByte(clean[2..4], 16),
                    Convert.ToByte(clean[4..6], 16)),
                8 => new ProviderBackgroundColor(
                    Convert.ToByte(clean[0..2], 16),
                    Convert.ToByte(clean[2..4], 16),
                    Convert.ToByte(clean[4..6], 16),
                    Convert.ToByte(clean[6..8], 16)),
                _ => ProviderBackgroundColor.White
            };
        }
        catch
        {
            return ProviderBackgroundColor.White;
        }
    }
}

/// <summary>
/// 공급자 간 배경색 전달 형식을 통일하기 위한 ARGB 값 구조체입니다.
/// </summary>
internal readonly record struct ProviderBackgroundColor(byte Alpha, byte Red, byte Green, byte Blue)
{
    public static ProviderBackgroundColor White => new(255, 255, 255, 255);
}
