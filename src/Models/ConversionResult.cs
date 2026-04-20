namespace PixConvert.Models;

/// <summary>
/// 변환 provider가 생성한 결과 파일 정보를 나타냅니다.
/// UI 바인딩 상태 반영은 ViewModel이 담당합니다.
/// </summary>
public sealed record ConversionResult(
    FileConvertStatus Status,
    string? OutputPath = null,
    long OutputSize = 0);
