using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NetVips;
using PixConvert.Models;
using PixConvert.Services.Interfaces;

namespace PixConvert.Services.Providers;

/// <summary>
/// NetVips 엔진을 사용하는 애니메이션 및 고압축 이미지 변환 공급자입니다.
/// 애니메이션(GIF, WebP)의 모든 프레임을 보존하며, libvips의 고성능 인코더를 활용합니다.
/// </summary>
public class NetVipsProvider : IProviderService, IDisposable
{
    private readonly ILanguageService _languageService;
    private readonly ILogger<NetVipsProvider> _logger;
    private bool _isDisposed;

    public string Name => "NetVips";

    public NetVipsProvider(ILanguageService languageService, ILogger<NetVipsProvider> logger)
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

        try
        {
            await Task.Run(() => ExecuteConversion(file, settings, outputPath, token), token);

            if (System.IO.File.Exists(outputPath))
            {
                file.OutputSize = new System.IO.FileInfo(outputPath).Length;
            }

            file.Progress = 100;
            file.OutputPath = outputPath;
            file.Status = FileConvertStatus.Success;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            file.Status = FileConvertStatus.Error;
            throw;
        }
    }

    private void ExecuteConversion(FileItem file, ConvertSettings settings, string outputPath, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        string targetFormat = file.IsAnimation
            ? settings.AnimationTargetFormat ?? throw new InvalidOperationException("AnimationTargetFormat is required for animation output.")
            : settings.StandardTargetFormat;

        var loaderOptions = new VOption();
        if (file.IsAnimation)
        {
            loaderOptions.Add("n", -1);
        }

        Image image = file.FileSignature.Equals("BMP", StringComparison.OrdinalIgnoreCase)
            ? LoadBmpViaSkia(file.Path)
            : Image.NewFromFile(file.Path, access: Enums.Access.Sequential, kwargs: loaderOptions);

        try
        {
            token.ThrowIfCancellationRequested();

            bool targetSupportsAlpha = targetFormat is "PNG" or "WEBP" or "AVIF" or "GIF";
            Image workImage = image;
            bool isNewImage = false;

            if (!targetSupportsAlpha && image.HasAlpha())
            {
                var bgColor = ParseBackgroundColor(settings, file.IsAnimation);
                workImage = image.Flatten(background: bgColor);
                isNewImage = true;
            }

            token.ThrowIfCancellationRequested();

            try
            {
                SaveWithFormat(workImage, outputPath, targetFormat, settings, file.IsAnimation);
            }
            finally
            {
                if (isNewImage) workImage.Dispose();
            }
        }
        finally
        {
            image.Dispose();
        }
    }

    private static void SaveWithFormat(Image image, string outputPath, string targetFormat, ConvertSettings settings, bool isAnimation)
    {
        int quality = GetQuality(settings, isAnimation);
        bool lossless = GetLossless(settings, isAnimation);

        switch (targetFormat.ToUpperInvariant())
        {
            case "BMP":
                SaveAsBmpViaSkia(image, outputPath);
                return;
            case "JPEG":
                image.Jpegsave(
                    outputPath,
                    q: quality,
                    subsampleMode: ResolveJpegSubsampleMode(settings, isAnimation),
                    keep: Enums.ForeignKeep.None);
                return;
            case "PNG":
                image.Pngsave(
                    outputPath,
                    compression: settings.StandardPngCompressionLevel,
                    filter: Enums.ForeignPngFilter.All,
                    keep: Enums.ForeignKeep.None);
                return;
            case "WEBP":
                SaveWebp(image, outputPath, settings, isAnimation, quality, lossless);
                return;
            case "AVIF":
                SaveAvif(image, outputPath, settings, isAnimation, quality, lossless);
                return;
            case "GIF":
                SaveGif(image, outputPath, settings, isAnimation);
                return;
            default:
                throw new NotSupportedException($"NetVipsProviderì—ì„œ ì§€ì›í•˜ì§€ ì•ŠëŠ” ëŒ€ìƒ í¬ë§·: {targetFormat}");
        }
    }

    private static void SaveWebp(Image image, string outputPath, ConvertSettings settings, bool isAnimation, int quality, bool lossless)
    {
        if (!isAnimation)
        {
            image.Webpsave(
                outputPath,
                q: lossless ? null : quality,
                lossless: lossless,
                keep: Enums.ForeignKeep.None);
            return;
        }

        if (lossless)
        {
            SaveLosslessWebp(image, outputPath, settings);
            return;
        }

        image.Webpsave(
            outputPath,
            q: quality,
            lossless: false,
            preset: ResolveWebpPreset(settings.AnimationWebpPreset),
            alphaQ: 100,
            effort: settings.AnimationWebpEncodingEffort,
            keep: Enums.ForeignKeep.None);
    }

    private static void SaveLosslessWebp(Image image, string outputPath, ConvertSettings settings)
    {
        try
        {
            image.Webpsave(
                outputPath,
                lossless: true,
                exact: settings.AnimationWebpPreserveTransparentPixels,
                effort: settings.AnimationWebpEncodingEffort,
                keep: Enums.ForeignKeep.None);
        }
        catch (ArgumentException ex) when (ex.Message.Contains("exact", StringComparison.OrdinalIgnoreCase))
        {
            // NetVips 3.2.0 exposes `exact`, but some bundled libvips/webp builds do not.
            image.Webpsave(
                outputPath,
                lossless: true,
                effort: settings.AnimationWebpEncodingEffort,
                keep: Enums.ForeignKeep.None);
        }
    }

    private static void SaveAvif(Image image, string outputPath, ConvertSettings settings, bool isAnimation, int quality, bool lossless)
    {
        Enums.ForeignSubsample subsampleMode = ResolveAvifSubsampleMode(settings, isAnimation, lossless);
        int effort = ResolveAvifEncodingEffort(settings, isAnimation);
        int? bitDepth = ResolveAvifBitDepth(settings, isAnimation);

        if (bitDepth.HasValue)
        {
            image.Heifsave(
                outputPath,
                q: lossless ? null : quality,
                bitdepth: bitDepth.Value,
                lossless: lossless,
                compression: Enums.ForeignHeifCompression.Av1,
                effort: effort,
                subsampleMode: subsampleMode,
                keep: Enums.ForeignKeep.None);
            return;
        }

        image.Heifsave(
            outputPath,
            q: lossless ? null : quality,
            lossless: lossless,
            compression: Enums.ForeignHeifCompression.Av1,
            effort: effort,
            subsampleMode: subsampleMode,
            keep: Enums.ForeignKeep.None);
    }

    private static void SaveGif(Image image, string outputPath, ConvertSettings settings, bool isAnimation)
    {
        var (dither, bitDepth) = ResolveGifPalettePreset(settings, isAnimation);

        image.Gifsave(
            outputPath,
            dither: dither,
            bitdepth: bitDepth,
            interframeMaxerror: ResolveGifInterframeMaxError(settings, isAnimation),
            interpaletteMaxerror: ResolveGifInterpaletteMaxError(settings, isAnimation),
            keep: Enums.ForeignKeep.None);
    }

    private static void SaveAsBmpViaSkia(Image vipsImage, string outputPath)
    {
        var flat = vipsImage.HasAlpha() ? vipsImage.Flatten() : vipsImage;

        try
        {
            byte[] pixels = flat.WriteToMemory<byte>();
            int w = flat.Width;
            int h = flat.Height;

            var info = new SkiaSharp.SKImageInfo(w, h, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Opaque);
            using var skBitmap = new SkiaSharp.SKBitmap(info);

            byte[] dstBytes = new byte[w * h * 4];
            var srcSpan = pixels.AsSpan();

            int totalPixels = w * h;
            for (int i = 0; i < totalPixels; i++)
            {
                int srcOff = i * 3;
                int dstOff = i * 4;
                dstBytes[dstOff] = srcSpan[srcOff + 2];
                dstBytes[dstOff + 1] = srcSpan[srcOff + 1];
                dstBytes[dstOff + 2] = srcSpan[srcOff];
                dstBytes[dstOff + 3] = 255;
            }

            System.Runtime.InteropServices.Marshal.Copy(dstBytes, 0, skBitmap.GetPixels(), dstBytes.Length);

            BmpEncoder.SaveAsync(skBitmap, outputPath).GetAwaiter().GetResult();
        }
        finally
        {
            if (flat != vipsImage) flat.Dispose();
        }
    }

    private static double[] ParseBackgroundColor(ConvertSettings settings, bool isAnimation)
    {
        string backgroundColor = settings.StandardBackgroundColor ?? "#FFFFFF";

        return TryParseHexToArray(backgroundColor);
    }

    private static double[] TryParseHexToArray(string hex)
    {
        try
        {
            string clean = hex.TrimStart('#');
            if (clean.Length == 6)
            {
                return
                [
                    Convert.ToByte(clean[0..2], 16),
                    Convert.ToByte(clean[2..4], 16),
                    Convert.ToByte(clean[4..6], 16)
                ];
            }
            if (clean.Length == 8)
            {
                return
                [
                    Convert.ToByte(clean[2..4], 16),
                    Convert.ToByte(clean[4..6], 16),
                    Convert.ToByte(clean[6..8], 16)
                ];
            }
        }
        catch
        {
        }

        return [255.0, 255.0, 255.0];
    }

    private static int GetQuality(ConvertSettings settings, bool isAnimation) =>
        isAnimation ? settings.AnimationQuality : settings.StandardQuality;

    private static bool GetLossless(ConvertSettings settings, bool isAnimation) =>
        isAnimation ? settings.AnimationLossless : settings.StandardLossless;

    private static Enums.ForeignSubsample ResolveJpegSubsampleMode(ConvertSettings settings, bool isAnimation)
    {
        if (isAnimation)
            return Enums.ForeignSubsample.Off;

        return settings.StandardJpegChromaSubsampling switch
        {
            JpegChromaSubsamplingMode.Chroma420 => Enums.ForeignSubsample.On,
            JpegChromaSubsamplingMode.Chroma444 => Enums.ForeignSubsample.Off,
            JpegChromaSubsamplingMode.Chroma422 => Enums.ForeignSubsample.Auto,
            _ => Enums.ForeignSubsample.Off
        };
    }

    private static Enums.ForeignSubsample ResolveAvifSubsampleMode(ConvertSettings settings, bool isAnimation, bool lossless)
    {
        if (isAnimation)
            return Enums.ForeignSubsample.Auto;

        if (lossless)
            return Enums.ForeignSubsample.Off;

        return settings.StandardAvifChromaSubsampling switch
        {
            AvifChromaSubsamplingMode.On => Enums.ForeignSubsample.On,
            AvifChromaSubsamplingMode.Off => Enums.ForeignSubsample.Off,
            _ => Enums.ForeignSubsample.Auto
        };
    }

    private static int ResolveAvifEncodingEffort(ConvertSettings settings, bool isAnimation) =>
        isAnimation ? 4 : settings.StandardAvifEncodingEffort;

    private static int? ResolveAvifBitDepth(ConvertSettings settings, bool isAnimation)
    {
        if (isAnimation)
            return null;

        return settings.StandardAvifBitDepth switch
        {
            AvifBitDepthMode.Bit8 => 8,
            AvifBitDepthMode.Bit10 => 10,
            AvifBitDepthMode.Bit12 => 12,
            _ => null
        };
    }

    private static (double Dither, int BitDepth) ResolveGifPalettePreset(ConvertSettings settings, bool isAnimation)
    {
        if (!isAnimation)
            return (0.0, 8);

        return settings.AnimationGifPalettePreset switch
        {
            GifPalettePreset.Vivid => (0.5, 8),
            GifPalettePreset.Balance => (0.3, 8),
            GifPalettePreset.Simple => (0.3, 7),
            GifPalettePreset.Minimal => (0.0, 6),
            _ => (0.0, 8)
        };
    }

    private static double ResolveGifInterframeMaxError(ConvertSettings settings, bool isAnimation) =>
        isAnimation ? settings.AnimationGifInterframeMaxError : 0.0;

    private static double ResolveGifInterpaletteMaxError(ConvertSettings settings, bool isAnimation) =>
        isAnimation ? settings.AnimationGifInterpaletteMaxError : 0.0;

    private static Enums.ForeignWebpPreset ResolveWebpPreset(WebpPresetMode preset) =>
        preset switch
        {
            WebpPresetMode.Picture => Enums.ForeignWebpPreset.Picture,
            WebpPresetMode.Photo => Enums.ForeignWebpPreset.Photo,
            WebpPresetMode.Drawing => Enums.ForeignWebpPreset.Drawing,
            WebpPresetMode.Icon => Enums.ForeignWebpPreset.Icon,
            WebpPresetMode.Text => Enums.ForeignWebpPreset.Text,
            _ => Enums.ForeignWebpPreset.Default
        };

    private static Image LoadBmpViaSkia(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var skBitmap = SkiaSharp.SKBitmap.Decode(stream);

        if (skBitmap == null)
            throw new InvalidOperationException($"SkiaSharp failed to decode BMP for NetVips: {path}");

        int w = skBitmap.Width;
        int h = skBitmap.Height;

        byte[] rgbPixels = new byte[w * h * 3];

        using var srcBitmap = skBitmap.Copy(SkiaSharp.SKColorType.Rgba8888);
        var span = srcBitmap.GetPixelSpan();

        int total = w * h;
        for (int i = 0; i < total; i++)
        {
            int srcOff = i * 4;
            int dstOff = i * 3;
            rgbPixels[dstOff] = span[srcOff];
            rgbPixels[dstOff + 1] = span[srcOff + 1];
            rgbPixels[dstOff + 2] = span[srcOff + 2];
        }

        return Image.NewFromMemory(rgbPixels, w, h, 3, Enums.BandFormat.Uchar);
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }
}
