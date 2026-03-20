using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SkiaSharp;
using PixConvert.Models;
using PixConvert.Services.Interfaces;

namespace PixConvert.Services.Providers;

/// <summary>
/// SkiaSharp 엔진을 사용하는 정지 이미지 변환 공급자입니다.
/// 지원 출력 포맷: JPEG / PNG / BMP / WebP
/// </summary>
public class SkiaSharpProvider : IProviderService, IDisposable
{
    private readonly ILanguageService _languageService;

    public SkiaSharpProvider(ILanguageService languageService)
    {
        _languageService = languageService;
    }

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

        SKBitmap? srcBitmap        = null;
        SKBitmap? compositedBitmap = null;

        try
        {
            token.ThrowIfCancellationRequested();

            // ── 2. 이미지 디코딩 ──────────────────────────────────────────────
            try
            {
                await using var inputStream = new FileStream(
                    file.Path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
                srcBitmap = SKBitmap.Decode(inputStream)
                    ?? throw new InvalidOperationException($"SKBitmap.Decode returned null: {file.Path}");
            }
            catch (Exception ex)
            {
                file.Status = FileConvertStatus.Error;
                var msg = string.Format(_languageService.GetString("Log_Skia_DecodeFail"), file.Path);
                throw new IOException(msg, ex);
            }

            token.ThrowIfCancellationRequested();

            // ── 3. 배경색 합성 ────────────────────────────────────────────────
            string targetFormat = file.IsAnimation
                ? settings.AnimationTargetFormat
                : settings.StandardTargetFormat;

            bool targetSupportsAlpha = targetFormat is "PNG" or "WEBP";
            bool needsCompositing    = !targetSupportsAlpha && HasAlphaChannel(srcBitmap);

            SKBitmap bitmapToEncode;
            if (needsCompositing)
            {
                compositedBitmap = CompositeBackground(srcBitmap, settings);
                bitmapToEncode   = compositedBitmap;
            }
            else
            {
                bitmapToEncode = srcBitmap;
            }

            // ── 4. 인코딩 및 파일 저장 ────────────────────────────────────────
            try
            {
                var (skFormat, quality) = ResolveEncodeParams(targetFormat, settings.Quality);

                // SKImage.FromBitmap()을 사용하여 인코딩 수행
                using var image = SKImage.FromBitmap(bitmapToEncode);
                using var data  = image.Encode(skFormat, quality);

                if (data is null)
                {
                    var msg = string.Format(_languageService.GetString("Log_Skia_EncodeFail"), targetFormat);
                    throw new InvalidOperationException(msg);
                }

                // 인코딩된 메모리 버퍼를 스트림에 쓰는 동기 호출 (데이터 복사 비용 미미)
                await using var outputStream = new FileStream(
                    outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
                data.SaveTo(outputStream);

                file.Progress = 100;
                file.Status = FileConvertStatus.Success;
            }
            catch (Exception ex)
            {
                file.Status = FileConvertStatus.Error;
                var msg = string.Format(_languageService.GetString("Log_Skia_SaveFail"), outputPath);
                throw new IOException(msg, ex);
            }
        }
        finally
        {
            // 명확한 소유권 기반 리소스 해제
            compositedBitmap?.Dispose();
            srcBitmap?.Dispose();
        }

        // ── 5. EXIF 보존 ──────────────────────────────────────────────────
        // TODO: settings.KeepExif == true 인 경우 NetVips 라우팅으로 처리 예정 (Phase B.5)
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Private helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>비트맵에 실제 유효한 알파 채널(투명도)이 있는지 확인합니다.</summary>
    private static bool HasAlphaChannel(SKBitmap bitmap) =>
        bitmap.AlphaType is SKAlphaType.Premul or SKAlphaType.Unpremul;

    /// <summary>
    /// 알파 채널을 지원하지 않는 출력 포맷용으로 배경색을 합성합니다.
    /// 결과 비트맵은 Opaque RGB이므로 호출자가 Dispose해야 합니다.
    /// </summary>
    private static SKBitmap CompositeBackground(SKBitmap src, ConvertSettings settings)
    {
        SKColor bgColor = ParseBackgroundColor(settings);

        var colorType = SKImageInfo.PlatformColorType;
        var dst = new SKBitmap(src.Width, src.Height, colorType, SKAlphaType.Opaque);
        using var canvas = new SKCanvas(dst);

        // 1. 배경 색상으로 채우기
        canvas.Clear(bgColor);

        // 2. 원본 이미지를 위에 합성 (알파 블렌딩 자동 적용)
        using var paint = new SKPaint { IsAntialias = false };
        canvas.DrawBitmap(src, 0, 0, paint);
        canvas.Flush();

        return dst;
    }

    /// <summary>설정에서 배경색 SKColor를 파싱합니다.</summary>
    private static SKColor ParseBackgroundColor(ConvertSettings settings)
    {
        return settings.BgColorOption switch
        {
            BackgroundColorOption.White  => SKColors.White,
            BackgroundColorOption.Black  => SKColors.Black,
            BackgroundColorOption.Custom => TryParseHexColor(settings.CustomBackgroundColor),
            _                           => SKColors.White
        };
    }

    /// <summary>
    /// "#RRGGBB" 또는 "#AARRGGBB" 형식의 HEX 문자열을 SKColor로 변환합니다.
    /// 파싱 실패 시 흰색(White)으로 폴백합니다.
    /// </summary>
    private static SKColor TryParseHexColor(string hex)
    {
        try
        {
            string clean = hex.TrimStart('#');
            return clean.Length switch
            {
                6 => new SKColor(
                        Convert.ToByte(clean[0..2], 16),
                        Convert.ToByte(clean[2..4], 16),
                        Convert.ToByte(clean[4..6], 16)),
                8 => new SKColor(
                        Convert.ToByte(clean[2..4], 16),
                        Convert.ToByte(clean[4..6], 16),
                        Convert.ToByte(clean[6..8], 16),
                        Convert.ToByte(clean[0..2], 16)),
                _ => SKColors.White
            };
        }
        catch
        {
            return SKColors.White;
        }
    }

    /// <summary>출력 포맷 문자열로부터 SKEncodedImageFormat과 Quality를 결정합니다.</summary>
    private static (SKEncodedImageFormat Format, int Quality) ResolveEncodeParams(
        string targetFormat, int quality)
    {
        return targetFormat.ToUpperInvariant() switch
        {
            "JPEG" => (SKEncodedImageFormat.Jpeg, quality),
            "PNG"  => (SKEncodedImageFormat.Png,  100),    // PNG는 무손실 → quality 무의미
            "WEBP" => (SKEncodedImageFormat.Webp, quality),
            _      => throw new NotSupportedException($"SkiaSharpProvider에서 지원하지 않는 대상 포맷: {targetFormat}")
        };
    }

    public void Dispose() { /* SkiaSharp은 인스턴스 레벨 네이티브 자원 없음 */ }
}
