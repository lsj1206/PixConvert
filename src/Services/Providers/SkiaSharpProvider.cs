using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using PixConvert.Models;
using PixConvert.Services.Interfaces;

namespace PixConvert.Services.Providers;

/// <summary>
/// SkiaSharp 엔진을 사용하는 정지 이미지 변환 공급자입니다.
/// 지원 출력 포맷: JPEG / PNG / BMP / WebP.
/// </summary>
public class SkiaSharpProvider : IProviderService, IDisposable
{
    private readonly ILanguageService _languageService;
    private readonly ILogger<SkiaSharpProvider> _logger;

    public string Name => "SkiaSharp";

    public SkiaSharpProvider(ILanguageService languageService, ILogger<SkiaSharpProvider> logger)
    {
        _languageService = languageService;
        _logger = logger;
    }

    public async Task ConvertAsync(FileItem file, ConvertSettings settings, ConversionSession session, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        string basePath = OutputPathResolver.Resolve(file, settings);
        var (outputPath, isCollision) = OutputPathResolver.ApplyOverwritePolicy(basePath, settings.OverwritePolicy, session, file.Path);

        if (isCollision && outputPath is not null)
        {
            _logger.LogWarning(_languageService.GetString("Log_Conversion_PathCollision"), outputPath);
        }

        if (outputPath is null)
        {
            file.Status = FileConvertStatus.Skipped;
            return;
        }

        string outputDir = Path.GetDirectoryName(outputPath)!;
        if (!string.IsNullOrEmpty(outputDir))
            Directory.CreateDirectory(outputDir);

        SKBitmap? srcBitmap = null;
        SKBitmap? compositedBitmap = null;

        try
        {
            token.ThrowIfCancellationRequested();

            try
            {
                await using var inputStream = new FileStream(
                    file.Path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
                srcBitmap = SKBitmap.Decode(inputStream)
                    ?? throw new InvalidOperationException($"SKBitmap.Decode returned null: {file.Path}");
            }
            catch (Exception)
            {
                file.Status = FileConvertStatus.Error;
                throw;
            }

            token.ThrowIfCancellationRequested();

            string targetFormat = file.IsAnimation
                ? settings.AnimationTargetFormat ?? throw new InvalidOperationException("AnimationTargetFormat is required for animation output.")
                : settings.StandardTargetFormat;

            bool targetSupportsAlpha = targetFormat is "PNG" or "WEBP";
            bool needsCompositing = !targetSupportsAlpha && HasAlphaChannel(srcBitmap);

            SKBitmap bitmapToEncode;
            if (needsCompositing)
            {
                compositedBitmap = CompositeBackground(srcBitmap, settings, file.IsAnimation);
                bitmapToEncode = compositedBitmap;
            }
            else
            {
                bitmapToEncode = srcBitmap;
            }

            try
            {
                if (targetFormat.Equals("BMP", StringComparison.OrdinalIgnoreCase))
                {
                    await BmpEncoder.SaveAsync(bitmapToEncode, outputPath);
                }
                else
                {
                    using var data = EncodeBitmap(bitmapToEncode, targetFormat, settings, file.IsAnimation);

                    await using var outputStream = new FileStream(
                        outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
                    data.SaveTo(outputStream);
                }

                if (System.IO.File.Exists(outputPath))
                {
                    file.OutputSize = new System.IO.FileInfo(outputPath).Length;
                }

                file.Progress = 100;
                file.OutputPath = outputPath;
                file.Status = FileConvertStatus.Success;
            }
            catch (Exception)
            {
                file.Status = FileConvertStatus.Error;
                throw;
            }
        }
        finally
        {
            compositedBitmap?.Dispose();
            srcBitmap?.Dispose();
        }
    }

    private static bool HasAlphaChannel(SKBitmap bitmap) =>
        bitmap.AlphaType is SKAlphaType.Premul or SKAlphaType.Unpremul;

    private static SKBitmap CompositeBackground(SKBitmap src, ConvertSettings settings, bool isAnimation)
    {
        SKColor bgColor = ParseBackgroundColor(settings, isAnimation);

        var colorType = SKImageInfo.PlatformColorType;
        var dst = new SKBitmap(src.Width, src.Height, colorType, SKAlphaType.Opaque);
        using var canvas = new SKCanvas(dst);

        canvas.Clear(bgColor);

        using var paint = new SKPaint { IsAntialias = false };
        canvas.DrawBitmap(src, 0, 0, paint);
        canvas.Flush();

        return dst;
    }

    private static SKColor ParseBackgroundColor(ConvertSettings settings, bool isAnimation)
    {
        string backgroundColor = settings.StandardBackgroundColor ?? "#FFFFFF";
        return TryParseHexColor(backgroundColor);
    }

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

    private SKData EncodeBitmap(SKBitmap bitmap, string targetFormat, ConvertSettings settings, bool isAnimation)
    {
        return targetFormat.ToUpperInvariant() switch
        {
            "JPEG" => EncodeJpeg(bitmap, settings, isAnimation),
            "PNG" => EncodePng(bitmap, settings),
            "WEBP" => EncodeWebp(bitmap, settings, isAnimation),
            _ => throw new NotSupportedException($"SkiaSharpProvider에서 지원하지 않는 대상 포맷: {targetFormat}")
        };
    }

    private SKData EncodeJpeg(SKBitmap bitmap, ConvertSettings settings, bool isAnimation)
    {
        using SKPixmap pixmap = bitmap.PeekPixels()
            ?? throw new InvalidOperationException(string.Format(_languageService.GetString("Log_Skia_EncodeFail"), "JPEG"));

        int quality = GetQuality(settings, isAnimation);
        var options = new SKJpegEncoderOptions(
            quality,
            ResolveJpegDownsample(settings, isAnimation),
            SKJpegEncoderAlphaOption.Ignore);

        return pixmap.Encode(options)
            ?? throw new InvalidOperationException(string.Format(_languageService.GetString("Log_Skia_EncodeFail"), "JPEG"));
    }

    private SKData EncodePng(SKBitmap bitmap, ConvertSettings settings)
    {
        using SKPixmap pixmap = bitmap.PeekPixels()
            ?? throw new InvalidOperationException(string.Format(_languageService.GetString("Log_Skia_EncodeFail"), "PNG"));

        var options = new SKPngEncoderOptions(
            SKPngEncoderFilterFlags.AllFilters,
            settings.StandardPngCompressionLevel);
        return pixmap.Encode(options)
            ?? throw new InvalidOperationException(string.Format(_languageService.GetString("Log_Skia_EncodeFail"), "PNG"));
    }

    private SKData EncodeWebp(SKBitmap bitmap, ConvertSettings settings, bool isAnimation)
    {
        using SKPixmap pixmap = bitmap.PeekPixels()
            ?? throw new InvalidOperationException(string.Format(_languageService.GetString("Log_Skia_EncodeFail"), "WEBP"));

        bool lossless = GetLossless(settings, isAnimation);
        int quality = GetQuality(settings, isAnimation);

        var options = lossless
            ? new SKWebpEncoderOptions(SKWebpEncoderCompression.Lossless, 100f)
            : new SKWebpEncoderOptions(SKWebpEncoderCompression.Lossy, quality);

        return pixmap.Encode(options)
            ?? throw new InvalidOperationException(string.Format(_languageService.GetString("Log_Skia_EncodeFail"), "WEBP"));
    }

    private static int GetQuality(ConvertSettings settings, bool isAnimation) =>
        isAnimation ? settings.AnimationQuality : settings.StandardQuality;

    private static bool GetLossless(ConvertSettings settings, bool isAnimation) =>
        isAnimation ? settings.AnimationLossless : settings.StandardLossless;

    private static SKJpegEncoderDownsample ResolveJpegDownsample(ConvertSettings settings, bool isAnimation)
    {
        if (isAnimation)
            return SKJpegEncoderDownsample.Downsample444;

        return settings.StandardJpegChromaSubsampling switch
        {
            JpegChromaSubsamplingMode.Chroma420 => SKJpegEncoderDownsample.Downsample420,
            JpegChromaSubsamplingMode.Chroma422 => SKJpegEncoderDownsample.Downsample422,
            JpegChromaSubsamplingMode.Chroma444 => SKJpegEncoderDownsample.Downsample444,
            _ => SKJpegEncoderDownsample.Downsample444
        };
    }

    public void Dispose() { }
}
