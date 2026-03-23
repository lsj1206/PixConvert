using System.Threading;
using System.Threading.Tasks;
using PixConvert.Models;

namespace PixConvert.Services.Interfaces;

/// <summary>
/// 이미지 변환을 수행하는 각 엔진(Provider)의 공통 인터페이스입니다.
/// </summary>
public interface IProviderService
{
    /// <summary>
    /// 지정된 설정에 따라 파일을 변환합니다.
    /// </summary>
    /// <param name="file">변환할 파일 정보</param>
    /// <param name="settings">변환 설정</param>
    /// <param name="session">병렬 덮어쓰기 충돌 방지용 경로 예약 세션</param>
    /// <param name="token">작업 취소 토큰</param>
    /// <returns>변환 작업 Task</returns>
    Task ConvertAsync(FileItem file, ConvertSettings settings, ConversionSession session, CancellationToken token);
}
