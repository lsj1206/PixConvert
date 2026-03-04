using System;
using System.Threading;
using System.Threading.Tasks;
using PixConvert.Models;
using PixConvert.Services.Interfaces;

namespace PixConvert.Services.Providers;

/// <summary>
/// SkiaSharp 엔진을 사용하는 정지 이미지 변환 공급자입니다.
/// </summary>
public class SkiaConversionProvider : IFileConversionService, IDisposable
{
    public Task ConvertAsync(FileItem file, ConvertSettings settings, CancellationToken token)
    {
        // TODO: SkiaSharp 기반 변환 로직 구현 (Step 4)
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        // 네이티브 리소스 해제 필요 시 구현
    }
}
