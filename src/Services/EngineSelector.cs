using System;
using System.Linq;
using System.Collections.Generic;
using PixConvert.Models;
using PixConvert.Services.Interfaces;
using PixConvert.Services.Providers;

namespace PixConvert.Services;

/// <summary>
/// 파일의 특성에 따라 적절한 변환 엔진(Provider)을 제공하는 팩토리 클래스입니다.
/// </summary>
public class EngineSelector
{
    private readonly SkiaSharpProvider _skiaSharp;
    private readonly NetVipsProvider _netVips;

    public EngineSelector(
        SkiaSharpProvider skiaSharp,
        NetVipsProvider netVips)
    {
        _skiaSharp = skiaSharp;
        _netVips = netVips;
    }

    /// <summary>
    /// 지정된 파일 아이템에 적합한 변환 서비스 구현체를 반환합니다
    /// 라우팅 정책:
    ///   - NetVips: GIF(애니메이션), AVIF(고압축), WebP-Animated(Step 4 구현 예정)
    ///   - SkiaSharp: JPEG, PNG, BMP, WEBP(정지)
    /// </summary>
    /// <param name="file">변환할 파일 아이템</param>
    /// <param name="settings">변환 설정 (출력 포맷 판단에 사용)</param>
    /// <returns>선택된 변환 서비스 공급자</returns>
    public IProviderService GetProvider(FileItem file, ConvertSettings settings)
    {
        // 1. 애니메이션 파일(GIF, WebP-Animated, AVIF-Sequence)은 NetVips로 라우팅
        if (file.IsAnimation)
            return _netVips;

        // 2. 정지 AVIF 입력 파일도 NetVips: 고압축 포맷 지원용
        if (file.FileSignature.Equals("AVIF", StringComparison.OrdinalIgnoreCase))
            return _netVips;

        // 3. AVIF 출력 포맷은 SkiaSharp 미지원 -> NetVips
        string targetFormat = file.IsAnimation
            ? settings.AnimationTargetFormat
            : settings.StandardTargetFormat;

        if (targetFormat.Equals("AVIF", StringComparison.OrdinalIgnoreCase) ||
            targetFormat.Equals("BMP", StringComparison.OrdinalIgnoreCase))
            return _netVips;

        // 4. 나머지 정지 이미지(JPEG, PNG, WEBP-Static)는 SkiaSharp
        return _skiaSharp;
    }
}
