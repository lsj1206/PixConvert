using System;
using System.IO;
using System.Collections.Generic;
using PixConvert.Models;

namespace PixConvert.Services.Providers;

/// <summary>
/// 변환 출력 파일의 경로를 결정하는 정적 헬퍼입니다.
/// SkiaSharpProvider, NetVipsProvider 양쪽에서 공유합니다.
/// </summary>
internal static class OutputPathResolver
{
    // 포맷 → 확장자 매핑
    private static readonly Dictionary<string, string> FormatToExtension =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["JPEG"] = "jpg",
            ["PNG"]  = "png",
            ["BMP"]  = "bmp",
            ["WEBP"] = "webp",
            ["AVIF"] = "avif",
            ["GIF"]  = "gif",
        };

    /// <summary>
    /// 입력 파일과 설정을 기반으로 출력 파일 경로(확장자 포함)를 반환합니다.
    /// 덮어쓰기 정책은 별도로 <see cref="ApplyOverwritePolicy"/>를 호출하여 적용합니다.
    /// </summary>
    public static string Resolve(FileItem file, ConvertSettings settings)
    {
        string targetFormat = file.IsAnimation
            ? settings.AnimationTargetFormat
            : settings.StandardTargetFormat;

        string ext = FormatToExtension.TryGetValue(targetFormat, out var e) ? e : targetFormat.ToLower();

        // 1. 기본 위치 결정
        string baseDir = settings.SaveLocation switch
        {
            SaveLocationType.SameAsOriginal => file.Directory,
            SaveLocationType.Custom => settings.CustomOutputPath,
            _ => file.Directory
        };

        // Custom 경로가 비어있을 경우 원본 폴더로 폴백
        if (string.IsNullOrWhiteSpace(baseDir))
            baseDir = file.Directory;

        // 2. 폴더 생성 전략 적용
        string outputDir = baseDir;
        if (settings.FolderMethod == SaveFolderMethod.CreateFolder)
        {
            // 토큰 치환 없이 설정된 이름을 그대로 사용 (v3 단순화)
            string subFolderName = string.IsNullOrWhiteSpace(settings.OutputSubFolderName)
                ? "PixConvert"
                : settings.OutputSubFolderName;

            outputDir = Path.Combine(baseDir, subFolderName);
        }

        return Path.Combine(outputDir, $"{System.IO.Path.GetFileNameWithoutExtension(file.Path)}.{ext}");
    }

    /// <summary>
    /// ConversionSession을 필수 파라미터로 받아
    /// 세션 단위 경로 예약을 강제합니다.
    /// session 없이 단독 호출하는 코드를 컴파일 시점에 차단합니다.
    /// 원본 파일 경로와 동일할 경우 과의도적 덮어쓰기로 인한 파괴를 방지하기 위해 Suffix로 전환합니다.
    /// </summary>
    /// <returns>(최종 출력 경로, 동일 세션 내 충돌 여부). Skip 조건이면 Path는 null.</returns>
    public static (string? Path, bool IsCollision) ApplyOverwritePolicy(
        string basePath,
        OverwritePolicy policy,
        ConversionSession session,
        string originalPath)
    {
        // 원본 파일 보호 메커니즘: 출력 경로가 원본과 동일할 때 Overwrite 금지
        if (policy == OverwritePolicy.Overwrite &&
            string.Equals(basePath, originalPath, StringComparison.OrdinalIgnoreCase))
        {
            policy = OverwritePolicy.Suffix;
        }

        switch (policy)
        {
            case OverwritePolicy.Overwrite:
            {
                var (path, collision) = session.ReserveForce(basePath);
                return (path, collision);
            }

            case OverwritePolicy.Skip:
            {
                bool ok = session.TryReserve(basePath);
                return ok ? (basePath, false) : (null, false);
            }

            case OverwritePolicy.Suffix:
            default:
            {
                string path = session.FindAndReserveSuffixed(basePath);
                return (path, false);
            }
        }
    }

}
