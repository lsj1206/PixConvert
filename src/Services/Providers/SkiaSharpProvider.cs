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
/// 지원 출력 포맷: JPEG / PNG / BMP / WebP
/// </summary>
public class SkiaSharpProvider : IProviderService, IDisposable
{
    private readonly ILanguageService _languageService;
    private readonly ILogger<SkiaSharpProvider> _logger;

    public SkiaSharpProvider(ILanguageService languageService, ILogger<SkiaSharpProvider> logger)
    {
        _languageService = languageService;
        _logger = logger;
    }

    public async Task ConvertAsync(FileItem file, ConvertSettings settings, ConversionSession session, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        // ── 1. 출력 경로 결정 ──────────────────────────────────────────────
        string basePath = OutputPathResolver.Resolve(file, settings);
        var (outputPath, isCollision) = OutputPathResolver.ApplyOverwritePolicy(basePath, settings.OverwriteSide, session, file.Path);

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
            catch (Exception)
            {
                file.Status = FileConvertStatus.Error;
                // 원본 예외 보존 (상위 ConversionViewModel에서 전체 예외 기록)
                throw;
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
                if (targetFormat.Equals("BMP", StringComparison.OrdinalIgnoreCase))
                {
                    // BMP는 SKImage.Encode가 null을 반환하므로 별도 고성능 경로로 처리
                    await SaveAsBmpAsync(bitmapToEncode, outputPath);
                }
                else
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
                }

                file.Progress = 100;
                file.Status = FileConvertStatus.Success;
            }
            catch (Exception)
            {
                file.Status = FileConvertStatus.Error;
                // 원본 예외 보존 (상위 ConversionViewModel에서 전체 예외 기록)
                throw;
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
            // BMP는 위의 SaveAsBmpAsync로 처리되므로 여기 도달하지 않음
            _      => throw new NotSupportedException($"SkiaSharpProvider에서 지원하지 않는 대상 포맷: {targetFormat}")
        };
    }

    /// <summary>
    /// SKImage.Encode가 BMP를 지원하지 않으므로(null 반환), BMP 이진 포맷을 직접 기록합니다.
    /// Bgra8888 픽셀 순서(Windows x64 기본값)가 BMP의 BGR 순서와 일치하므로
    /// 바이트 변환 없이 B, G, R 순으로 직접 읽을 수 있습니다.
    /// </summary>
    private static async Task SaveAsBmpAsync(SKBitmap src, string outputPath)
    {
        // PlatformColorType(Bgra8888)으로 강제 변환하여 바이트 순서를 고정
        bool wasCopied = src.ColorType != SKImageInfo.PlatformColorType;
        SKBitmap bmp = wasCopied ? src.Copy(SKImageInfo.PlatformColorType) : src;

        try
        {
            int w          = bmp.Width;
            int h          = bmp.Height;
            int rowBytes24 = ((w * 3 + 3) / 4) * 4;  // 4바이트 패딩
            int pixelSize  = rowBytes24 * h;
            int fileSize   = 54 + pixelSize;           // 14 + 40 + pixel data

            await using var fs = new FileStream(
                outputPath, FileMode.Create, FileAccess.Write,
                FileShare.None, 81920, useAsync: true);

            // BinaryWriter는 fs를 소유하지 않도록 설정
            using var bw = new BinaryWriter(fs, System.Text.Encoding.ASCII, leaveOpen: true);

            // ── BITMAPFILEHEADER (14 bytes) ─────────────────────────────
            bw.Write(new byte[] { (byte)'B', (byte)'M' }); // 시그니처
            bw.Write(fileSize);                             // bfSize
            bw.Write(0);                                    // bfReserved
            bw.Write(54);                                   // bfOffBits

            // ── BITMAPINFOHEADER (40 bytes) ─────────────────────────────
            bw.Write(40);         // biSize
            bw.Write(w);          // biWidth
            bw.Write(h);          // biHeight (양수 = bottom-up)
            bw.Write((short)1);   // biPlanes
            bw.Write((short)24);  // biBitCount (24-bit RGB)
            bw.Write(0);          // biCompression (BI_RGB)
            bw.Write(pixelSize);  // biSizeImage
            bw.Write(2835);       // biXPelsPerMeter (~72 DPI)
            bw.Write(2835);       // biYPelsPerMeter
            bw.Write(0);          // biClrUsed
            bw.Write(0);          // biClrImportant
            bw.Flush();

            // ── 픽셀 데이터 (bottom-to-top, BGR, 행 패딩) ──────────────
            int    srcStride = bmp.RowBytes;         // 원본 행 바이트 수 (w * 4)
            byte[] rowBuf    = new byte[rowBytes24]; // 출력 행 버퍼 (zero-initialized = 패딩 포함)

            for (int y = h - 1; y >= 0; y--)            // bottom-to-top
            {
                CopyRow(y, rowBuf, bmp, srcStride, w);
                // rowBuf의 나머지(패딩)는 0으로 초기화된 상태 유지
                await fs.WriteAsync(rowBuf, 0, rowBytes24);
            }
        }
        finally
        {
            if (wasCopied) bmp.Dispose();
        }

        // ReadOnlySpan(ref struct)은 await 지점을 넘나들 수 없으므로 별도 로컬 함수로 분리
        static void CopyRow(int y, byte[] buffer, SKBitmap bitmap, int stride, int width)
        {
            ReadOnlySpan<byte> span = bitmap.GetPixelSpan();
            int srcRow = y * stride;
            int dstOff = 0;
            for (int x = 0; x < width; x++)
            {
                int src4 = srcRow + x * 4;
                buffer[dstOff++] = span[src4];     // B
                buffer[dstOff++] = span[src4 + 1]; // G
                buffer[dstOff++] = span[src4 + 2]; // R
            }
        }
    }

    public void Dispose() { /* SkiaSharp은 인스턴스 레벨 네이티브 자원 없음 */ }
}
