using System;
using System.IO;
using System.Collections.Generic;
using PixConvert.Models;

namespace PixConvert.Services.Providers;

/// <summary>
/// 변환 출력 파일의 경로를 결정하는 정적 헬퍼입니다.
/// SkiaSharpProvider, NetVipsProvider 양쪽에서 공유합니다.
/// </summary>
public static class OutputPathResolver
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
        string baseDir = settings.OutputLocation switch
        {
            OutputLocationType.SameAsOriginal => file.Directory,
            OutputLocationType.Custom => settings.CustomOutputPath,
            _ => file.Directory
        };

        // Custom 경로가 비어있을 경우 원본 폴더로 폴백
        if (string.IsNullOrWhiteSpace(baseDir))
            baseDir = file.Directory;

        // 2. 폴더 생성 전략 적용
        string outputDir = baseDir;
        if (settings.FolderStrategy == OutputFolderStrategy.CreateFolder)
        {
            string subFolderName = ReplaceTokens(settings.OutputSubFolderName);
            outputDir = Path.Combine(baseDir, subFolderName);
        }

        return Path.Combine(outputDir, $"{System.IO.Path.GetFileNameWithoutExtension(file.Path)}.{ext}");
    }

    /// <summary>
    /// 폴더 이름 내의 토큰({yyyy-MM-dd} 등)을 현재 시간 값으로 치환합니다.
    /// </summary>
    private static string ReplaceTokens(string template)
    {
        if (string.IsNullOrWhiteSpace(template))
            return "PixConvert_Output";

        var now = DateTime.Now;
        return template
            .Replace("{yyyy-MM-dd}", now.ToString("yyyy-MM-dd"))
            .Replace("{yyyyMMdd}", now.ToString("yyyyMMdd"))
            .Replace("{yyyy}", now.ToString("yyyy"))
            .Replace("{MM}", now.ToString("MM"))
            .Replace("{dd}", now.ToString("dd"))
            .Replace("{HHmmss}", now.ToString("HHmmss"))
            .Replace("{HH}", now.ToString("HH"))
            .Replace("{mm}", now.ToString("mm"));
    }

    /// <summary>
    /// 덮어쓰기 정책에 따라 최종 출력 경로를 결정합니다.
    /// Skip 정책이고 파일이 이미 존재하면 null을 반환합니다.
    /// </summary>
    /// <returns>최종 출력 경로. Skip 조건이면 null.</returns>
    public static string? ApplyOverwritePolicy(string basePath, OverwritePolicy policy)
    {
        if (!File.Exists(basePath))
            return basePath;

        return policy switch
        {
            OverwritePolicy.Overwrite => basePath,
            OverwritePolicy.Skip      => null,
            OverwritePolicy.Suffix    => GenerateSuffixedPath(basePath),
            _                         => basePath
        };
    }

    // ─── Private helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// 같은 이름 파일이 존재할 때 _1, _2 ... 접미사를 붙여 비어 있는 경로를 찾습니다.
    /// 최대 9999회 시도 후 포기하면 타임스탬프 접미사로 폴백합니다.
    /// </summary>
    private static string GenerateSuffixedPath(string basePath)
    {
        string dir  = Path.GetDirectoryName(basePath) ?? string.Empty;
        string name = Path.GetFileNameWithoutExtension(basePath);
        string ext  = Path.GetExtension(basePath); // ".jpg" 형태

        for (int i = 1; i <= 9999; i++)
        {
            string candidate = Path.Combine(dir, $"{name}_{i}{ext}");
            if (!File.Exists(candidate))
                return candidate;
        }

        // 폴백: 타임스탬프 접미사
        return Path.Combine(dir, $"{name}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}{ext}");
    }
}
