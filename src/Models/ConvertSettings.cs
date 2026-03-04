namespace PixConvert.Models;

/// <summary>
/// 이미지 변환 시 사용되는 옵션(포맷, 품질, 저장 정책 등)을 담는 데이터 모델입니다.
/// </summary>
public class ConvertSettings
{
    /// <summary>변환하고자 하는 대상 확장자 (점 제외)</summary>
    public string TargetExtension { get; set; } = "png";

    /// <summary>이미지 품질 (1~100)</summary>
    public int Quality { get; set; } = 85;

    /// <summary>동일한 이름의 파일이 존재할 경우 덮어쓰기 여부</summary>
    public bool Overwrite { get; set; } = false;

    /// <summary>EXIF 메타데이터 보존 여부</summary>
    public bool KeepExif { get; set; } = false;

    /// <summary>투명 비트맵 배경 채우기 색상 (예: #FFFFFF)</summary>
    public string BackgroundColor { get; set; } = "#FFFFFF";

    // TODO: 기획서에 명시된 출력 경로 옵션, CPU 점유율 등은 Step 2에서 구체화
}
