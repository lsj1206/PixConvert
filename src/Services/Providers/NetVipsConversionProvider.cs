using System;
using System.Threading;
using System.Threading.Tasks;
using PixConvert.Models;
using PixConvert.Services.Interfaces;

namespace PixConvert.Services.Providers;

/// <summary>
/// NetVips 엔진을 사용하는 애니메이션 및 고압축 이미지 변환 공급자입니다.
/// </summary>
public class NetVipsConversionProvider : IFileConversionService, IDisposable
{
    public Task ConvertAsync(FileItem file, ConvertSettings settings, CancellationToken token)
    {
        // TODO: NetVips 기반 변환 로직 구현 (Step 4)
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        // libvips 네이티브 메모리 해제 로직 구현 필요 시 추가
    }
}
