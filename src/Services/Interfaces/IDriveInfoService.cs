namespace PixConvert.Services;

/// <summary>
/// 파일 경로의 드라이브 유형을 판별하여 최적 병렬 처리 수준을 제공하는 서비스입니다.
/// WMI 기반 판별 로직을 FileAnalyzerService로부터 분리하여 단독 테스트 및 재사용을 가능하게 합니다.
/// </summary>
public interface IDriveInfoService
{
    /// <summary>
    /// 지정된 파일 경로가 위치한 드라이브를 비동기로 분석하여 최적의 병렬 처리 수를 반환합니다.
    /// 드라이브 유형(Network / Removable / Fixed SSD / Fixed HDD)에 따라 값이 달라집니다.
    /// </summary>
    /// <param name="samplePath">드라이브 판별의 기준이 되는 파일 또는 폴더 경로</param>
    /// <returns>권장 최대 병렬 처리 수 (MaxDegreeOfParallelism에 직접 사용)</returns>
    Task<int> GetOptimalParallelismAsync(string samplePath);
}
