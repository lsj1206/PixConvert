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

        // ── 1. 출력 경로 결정 ──────────────────────────────────────────────
        string basePath = OutputPathResolver.Resolve(file, settings);
        var (outputPath, isCollision) = OutputPathResolver.ApplyOverwritePolicy(basePath, settings.OverwritePolicy, session, file.Path);

        if (isCollision && outputPath is not null)
        {
            _logger.LogWarning(_languageService.GetString("Log_Conversion_PathCollision"), outputPath);
        }

        // Overwrite 정책에서 세션 내 동명 파일 충돌 발생 시 경고 로그
        // ILogger가 없으므로 생략 또는 추가 분석, 현재 로거를 DI받지 않았으므로 생략
        // (Serilog 로거를 쓸 수도 있으나, 현재 NetVipsProvider 생성자엔 ILogger 없음)

        if (outputPath is null)
        {
            file.Status = FileConvertStatus.Skipped;
            return;
        }

        string outputDir = Path.GetDirectoryName(outputPath)!;
        if (!string.IsNullOrEmpty(outputDir))
            Directory.CreateDirectory(outputDir);

        // ── 2. 실제 변환 프로세스 격리 (Task.Run) ─────────────────────────
        // libvips의 동기 I/O 및 내부 스레드 관리를 .NET 스레드풀의 특정 스레드로 격리하여 보호합니다.
        try
        {
            await Task.Run(() => ExecuteConversion(file, settings, outputPath, token), token);

            // 변환 완료 후 결과 파일 크기 측정
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
            // 취소 시 상태 변경 없이 상위로 전파
            throw;
        }
        catch (Exception)
        {
            file.Status = FileConvertStatus.Error;
            // 원본 예외와 스택 트레이스를 보존하기 위해 그대로 전파
            // 상위의 ConversionViewModel.LogError가 전체 정보를 Serilog에 기록함
            throw;
        }
    }

    /// <summary>
    /// 실제 변환 로직을 수행하는 내부 동기 메서드입니다. (Task.Run 내부에서 실행)
    /// </summary>
    private void ExecuteConversion(FileItem file, ConvertSettings settings, string outputPath, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        string targetFormat = file.IsAnimation
            ? settings.AnimationTargetFormat
            : settings.StandardTargetFormat;

        // n: -1 -> 애니메이션 이미지의 모든 프레임을 로드 (GIF, WebP 등에서만 지원)
        // access: Sequential -> 메모리 사용량 최소화
        var loaderOptions = new VOption();
        if (file.IsAnimation)
        {
            loaderOptions.Add("n", -1);
        }

        var accessMode = file.FileSignature.Equals("BMP", StringComparison.OrdinalIgnoreCase)
            ? Enums.Access.Random
            : Enums.Access.Sequential;

        using var image = Image.NewFromFile(file.Path, access: accessMode, kwargs: loaderOptions);

        token.ThrowIfCancellationRequested();

        // ── 3. 배경 합성 (알파 미지원 포맷 대응) ──────────────────────────
        bool targetSupportsAlpha = targetFormat is "PNG" or "WEBP" or "AVIF" or "GIF";
        Image workImage = image;
        bool isNewImage = false;

        if (!targetSupportsAlpha && image.HasAlpha())
        {
            var bgColor = ParseBackgroundColor(settings);
            // Flatten은 이미지를 배경색과 합성하여 알파 채널을 제거함
            workImage = image.Flatten(background: bgColor);
            isNewImage = true;
        }

        token.ThrowIfCancellationRequested();

        // ── 4. 포맷별 전용 세이버(Saver) 호출 ──────────────────────────────
        try
        {
            SaveWithFormat(workImage, outputPath, targetFormat, settings);
        }
        finally
        {
            // 합성 등으로 생성된 중간 객체 해제
            if (isNewImage) workImage.Dispose();
        }
    }

    /// <summary>
    /// libvips의 포맷별 상세 옵션을 적용하여 저장합니다.
    /// </summary>
    private static void SaveWithFormat(Image image, string outputPath, string targetFormat, ConvertSettings settings)
    {
        var options = new VOption();

        switch (targetFormat.ToUpperInvariant())
        {
            case "BMP":
                SaveAsBmpViaSkia(image, outputPath);
                return;
            case "JPEG":
                options.Add("Q", settings.Quality);
                options.Add("strip", true);
                break;
            case "PNG":
                options.Add("compression", 6);
                break;
            case "WEBP":
                options.Add("Q", settings.Quality);
                options.Add("strip", true);
                break;
            case "AVIF":
                options.Add("compression", Enums.ForeignHeifCompression.Av1);
                options.Add("Q", settings.Quality);
                // libvips의 heifsave는 정지 이미지 저장 시 이 옵션들을 사용함
                break;
            // GIF, WEBP: WriteToFile 시 확장자 기반으로 n-pages 메타데이터를 자동 참조하여 애니메이션 저장됨
        }

        // WriteToFile은 파일 확장자에 따라 적절한 save 오퍼레이션을 선택하고 options를 전달함
        image.WriteToFile(outputPath, options);
    }

    private static void SaveAsBmpViaSkia(Image vipsImage, string outputPath)
    {
        // 알파 채널 제거 (BMP는 알파 미지원)
        var flat = vipsImage.HasAlpha() ? vipsImage.Flatten() : vipsImage;

        // NetVips 픽셀 버퍼 (RGB 8-bit packed) → SKBitmap (Rgb888x) 변환
        byte[] pixels = flat.WriteToMemory();
        int w = flat.Width;
        int h = flat.Height;

        var info = new SkiaSharp.SKImageInfo(w, h, SkiaSharp.SKColorType.Rgb888x, SkiaSharp.SKAlphaType.Opaque);
        using var skBitmap = new SkiaSharp.SKBitmap(info);

        byte[] dstBytes = new byte[w * h * 4];
        var srcSpan = pixels.AsSpan();

        int totalPixels = w * h;
        for (int i = 0; i < totalPixels; i++)
        {
            int srcOff = i * 3;
            int dstOff = i * 4;
            dstBytes[dstOff]     = srcSpan[srcOff + 2]; // B
            dstBytes[dstOff + 1] = srcSpan[srcOff + 1]; // G
            dstBytes[dstOff + 2] = srcSpan[srcOff];     // R
            dstBytes[dstOff + 3] = 255;                 // X
        }
        
        System.Runtime.InteropServices.Marshal.Copy(dstBytes, 0, skBitmap.GetPixels(), dstBytes.Length);

        BmpEncoder.SaveAsync(skBitmap, outputPath).GetAwaiter().GetResult();

        if (flat != vipsImage) flat.Dispose();
    }

    private static double[] ParseBackgroundColor(ConvertSettings settings)
    {
        return TryParseHexToArray(settings.BackgroundColor ?? "#FFFFFF");
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
            if (clean.Length == 8) // AARRGGBB
            {
                return new[]
                {
                    (double)Convert.ToByte(clean[2..4], 16),
                    (double)Convert.ToByte(clean[4..6], 16),
                    (double)Convert.ToByte(clean[6..8], 16)
                };
            }
        }
        catch { /* Fallback to white */ }
        return new[] { 255.0, 255.0, 255.0 };
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        // 인스턴스 레벨의 NetVips 자원 명시적 해제는 불필요 (루컬 using 블록에서 처리)
        GC.SuppressFinalize(this);
    }
}
