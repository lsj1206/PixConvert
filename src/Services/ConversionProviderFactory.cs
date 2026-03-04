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
public class ConversionProviderFactory
{
    private readonly SkiaConversionProvider _skiaProvider;
    private readonly NetVipsConversionProvider _netVipsProvider;

    public ConversionProviderFactory(
        SkiaConversionProvider skiaProvider,
        NetVipsConversionProvider netVipsProvider)
    {
        _skiaProvider = skiaProvider;
        _netVipsProvider = netVipsProvider;
    }

    /// <summary>
    /// 지정된 파일 아이템에 적합한 변환 서비스 구현체를 반환합니다.
    /// </summary>
    /// <param name="file">변환할 파일 아이템</param>
    /// <returns>선택된 변환 서비스 공급자</returns>
    public IFileConversionService GetProvider(FileItem file)
    {
        string signature = file.FileSignature.ToLower();
        string extension = file.Extension.ToLower();

        // 1. 애니메이션 포맷 (GIF)
        if (signature == "gif" || extension == "gif")
        {
            return _netVipsProvider;
        }

        // 2. 고성능/최신 포맷 (AVIF)
        if (signature == "avif" || extension == "avif")
        {
            return _netVipsProvider;
        }

        // 3. TODO: 애니메이션 WebP 판별 로직 추가 필요 (현재는 일반 WebP로 간주하여 Skia 사용 가능성 높음)
        // MVP 수준에서는 GIF, AVIF 외에는 Skia를 기본으로 사용

        return _skiaProvider;
    }
}
