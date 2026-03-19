using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NetVips;
using PixConvert.Models;
using PixConvert.Services.Interfaces;

namespace PixConvert.Services.Providers;

/// <summary>
/// NetVips 엔진을 사용하는 애니메이션 및 고압축 이미지 변환 공급자입니다.
/// Phase B에서는 SkiaSharp에서 처리하지 못하는 AVIF(인코딩)와 BMP(인코더 누락 대응)를 처리합니다.
/// </summary>
public class NetVipsProvider : IProviderService, IDisposable
{
    public async Task ConvertAsync(FileItem file, ConvertSettings settings, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        // ── 1. 출력 경로 결정 ──────────────────────────────────────────────
        string basePath    = OutputPathResolver.Resolve(file, settings);
        string? outputPath = OutputPathResolver.ApplyOverwritePolicy(basePath, settings.OverwriteSide);

        if (outputPath is null)
        {
            file.Status = FileConvertStatus.Success;
            return;
        }

        string outputDir = Path.GetDirectoryName(outputPath)!;
        if (!string.IsNullOrEmpty(outputDir))
            Directory.CreateDirectory(outputDir);

        try
        {
            token.ThrowIfCancellationRequested();

            // ── 2. 이미지 로드 및 처리 ────────────────────────────────────────
            // libvips는 지연 로딩을 지원하며, 로드 시점에 취소를 직접 제어하기 어려우므로 
            // 파일 읽기 전에 미리 체크함
            using var image = Image.NewFromFile(file.Path, access: Enums.Access.Random);

            string targetFormat = file.IsAnimation
                ? settings.AnimationTargetFormat
                : settings.StandardTargetFormat;

            Image processedImage = image;
            bool isNewImage = false;

            // ── 3. 배경색 합성 (BMP 인코딩 등 알파 미지원 포맷 대응) ──────────
            if (targetFormat == "BMP" && image.HasAlpha())
            {
                var bgColor = ParseBackgroundColor(settings);
                processedImage = image.Flatten(background: bgColor);
                isNewImage = true;
            }

            token.ThrowIfCancellationRequested();

            // ── 4. 파일 저장 ──────────────────────────────────────────────────
            // libvips는 확장자에 따라 자동으로 적절한 세이버(Saver)를 선택함
            processedImage.WriteToFile(outputPath);

            if (isNewImage)
                processedImage.Dispose();

            file.Status = FileConvertStatus.Success;
        }
        catch (Exception ex)
        {
            file.Status = FileConvertStatus.Error;
            if (ex is OperationCanceledException) throw;
            throw new IOException($"NetVips 변환 작업 실패: {file.Path}", ex);
        }
    }

    private static double[] ParseBackgroundColor(ConvertSettings settings)
    {
        // NetVips는 [R, G, B] 형태의 double 배열을 사용함 (0.0 ~ 255.0)
        // 기본값: White
        return settings.BgColorOption switch
        {
            BackgroundColorOption.White  => new[] { 255.0, 255.0, 255.0 },
            BackgroundColorOption.Black  => new[] { 0.0, 0.0, 0.0 },
            BackgroundColorOption.Custom => TryParseHexToArray(settings.CustomBackgroundColor),
            _                           => new[] { 255.0, 255.0, 255.0 }
        };
    }

    private static double[] TryParseHexToArray(string hex)
    {
        try
        {
            string clean = hex.TrimStart('#');
            if (clean.Length == 6)
            {
                return new[]
                {
                    (double)Convert.ToByte(clean[0..2], 16),
                    (double)Convert.ToByte(clean[2..4], 16),
                    (double)Convert.ToByte(clean[4..6], 16)
                };
            }
            if (clean.Length == 8) // AARRGGBB -> Ignore Alpha for flattening
            {
                return new[]
                {
                    (double)Convert.ToByte(clean[2..4], 16),
                    (double)Convert.ToByte(clean[4..6], 16),
                    (double)Convert.ToByte(clean[6..8], 16)
                };
            }
        }
        catch { /* Fallback */ }
        return new[] { 255.0, 255.0, 255.0 };
    }

    public void Dispose() { /* NetVips 모듈 레벨 자원 없음 */ }
}
