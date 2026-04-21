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
public class SkiaSharpProvider : IProviderService
{
    private readonly ILanguageService _languageService;
    private readonly ILogger<SkiaSharpProvider> _logger;

    public string Name => "SkiaSharp";

    public SkiaSharpProvider(ILanguageService languageService, ILogger<SkiaSharpProvider> logger)
    {
        _languageService = languageService;
        _logger = logger;
    }

    public async Task<ConversionResult> ConvertAsync(FileItem file, ConvertSettings settings, ConversionSession session, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        string? outputPath = ProviderConversionHelper.PrepareOutputPath(file, settings, session, _logger, _languageService);
        if (outputPath is null)
            return new ConversionResult(FileConvertStatus.Skipped);

        SKBitmap? srcBitmap = null;
        SKBitmap? compositedBitmap = null;

        try
        {
            token.ThrowIfCancellationRequested();

            await using var inputStream = new FileStream(
                file.Path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
            srcBitmap = SKBitmap.Decode(inputStream)
                ?? throw new InvalidOperationException($"SKBitmap.Decode returned null: {file.Path}");

            token.ThrowIfCancellationRequested();

            string targetFormat = ProviderConversionHelper.ResolveTargetFormat(file, settings);

            bool targetSupportsAlpha = targetFormat is "PNG" or "WEBP";
            bool needsCompositing = !targetSupportsAlpha && HasAlphaChannel(srcBitmap);

            SKBitmap bitmapToEncode;
            if (needsCompositing)
            {
                compositedBitmap = CompositeBackground(srcBitmap, settings);
                bitmapToEncode = compositedBitmap;
            }
            else
            {
                bitmapToEncode = srcBitmap;
            }

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

            return new ConversionResult(
                FileConvertStatus.Success,
                outputPath,
                ProviderConversionHelper.GetOutputSize(outputPath));
        }
        finally
        {
            compositedBitmap?.Dispose();
            srcBitmap?.Dispose();
        }
    }

    private static bool HasAlphaChannel(SKBitmap bitmap) =>
        bitmap.AlphaType is SKAlphaType.Premul or SKAlphaType.Unpremul;

    private static SKBitmap CompositeBackground(SKBitmap src, ConvertSettings settings)
    {
        SKColor bgColor = ParseBackgroundColor(settings);

        var colorType = SKImageInfo.PlatformColorType;
        var dst = new SKBitmap(src.Width, src.Height, colorType, SKAlphaType.Opaque);
        using var canvas = new SKCanvas(dst);

        canvas.Clear(bgColor);

        using var paint = new SKPaint { IsAntialias = false };
        canvas.DrawBitmap(src, 0, 0, paint);
        canvas.Flush();

        return dst;
    }

    private static SKColor ParseBackgroundColor(ConvertSettings settings)
    {
        ProviderBackgroundColor backgroundColor = ProviderConversionHelper.GetBackgroundColor(settings);
        return new SKColor(backgroundColor.Red, backgroundColor.Green, backgroundColor.Blue, backgroundColor.Alpha);
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

        int quality = ProviderConversionHelper.GetQuality(settings, isAnimation);
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

        bool lossless = ProviderConversionHelper.GetLossless(settings, isAnimation);
        int quality = ProviderConversionHelper.GetQuality(settings, isAnimation);

        var options = lossless
            ? new SKWebpEncoderOptions(SKWebpEncoderCompression.Lossless, 100f)
            : new SKWebpEncoderOptions(SKWebpEncoderCompression.Lossy, quality);

        return pixmap.Encode(options)
            ?? throw new InvalidOperationException(string.Format(_languageService.GetString("Log_Skia_EncodeFail"), "WEBP"));
    }

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
}
