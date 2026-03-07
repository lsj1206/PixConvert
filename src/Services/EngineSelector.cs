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
    /// <returns>선택된 변환 서비스 공급자</returns>
    public IProviderService GetProvider(FileItem file)
    {
        string signature = file.FileSignature.ToUpper();

        switch (signature)
        {
            // NetVips 경로: 애니메이션 및 고압축 포맷
            case "GIF":
                return _netVips;

            case "AVIF":
                return _netVips;

            // TODO: WebP-Animated 판별 로직 추가 필요 (Step 4 구현 예정)
            // 현재 WEBP는 헤더만으로 정지/애니메이션 구분 불가 → 기본 SkiaSharp 사용
            // 실제 구현 시: VP8X 청크의 Animation 플래그를 확인하여 분기 필요

            // SkiaSharp 경로: 정지 이미지 포맷 (JPEG, PNG, BMP, WEBP-Static)
            default:
                return _skiaSharp;
        }
    }
}
