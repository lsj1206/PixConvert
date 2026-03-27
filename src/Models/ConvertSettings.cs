namespace PixConvert.Models;

/// <summary>
/// 이미지 변환 시 사용되는 옵션(포맷, 품질, 저장 정책 등)을 담는 데이터 모델입니다.
/// </summary>
public class ConvertSettings
{
    /// <summary>이미지 품질 (1~100)</summary>
    public int Quality { get; set; } = 85;

    /// <summary>동일한 이름의 파일이 존재할 경우 처리 방침</summary>
    public OverwritePolicy OverwriteSide { get; set; } = OverwritePolicy.Suffix;

    /// <summary>EXIF 메타데이터 보존 여부</summary>
    public bool KeepExif { get; set; } = false;

    /// <summary>투명 비트맵 배경 채우기 방식</summary>
    public BackgroundColorOption BgColorOption { get; set; } = BackgroundColorOption.White;

    /// <summary>사용자 지정 배경 색상 (예: #FFFFFF)</summary>
    public string CustomBackgroundColor { get; set; } = "#FFFFFF";

    // --- 신규 설계 반영 (포맷 유형별 매핑) ---

    // 1. 일반 이미지 (Standard: JPEG, PNG, BMP, WEBP, AVIF)

    /// <summary>일반 이미지를 변환할 목표 확장자 (예: JPEG)</summary>
    public string StandardTargetFormat { get; set; } = "JPEG";

    // 2. 애니메이션 이미지 (Animation: GIF, WebP-Ani, AVIF-Seq)

    /// <summary>애니메이션(움짤)을 변환할 목표 확장자 (예: GIF)</summary>
    public string AnimationTargetFormat { get; set; } = "GIF";

    // --- 출력 및 성능 옵션 (Step 2 상세화) ---

    /// <summary>출력 저장 위치 방식</summary>
    public OutputLocationType OutputLocation { get; set; } = OutputLocationType.SameAsOriginal;

    /// <summary>하위 폴더 생성 정책</summary>
    public OutputFolderStrategy FolderStrategy { get; set; } = OutputFolderStrategy.CreateFolder;

    /// <summary>하위 폴더 이름 (필터/토큰 지원)</summary>
    public string OutputSubFolderName { get; set; } = "PixConvert";

    /// <summary>사용자 지정 출력 경로</summary>
    public string CustomOutputPath { get; set; } = string.Empty;

    /// <summary>CPU 작업 부하 옵션</summary>
    public CpuUsageOption CpuUsage { get; set; } = CpuUsageOption.Optimal;
}

/// <summary>출력 저장 위치 유형</summary>
public enum OutputLocationType { SameAsOriginal, Custom }

/// <summary>하위 폴더 생성 전략</summary>
public enum OutputFolderStrategy { NoFolder, CreateFolder }

/// <summary>배경색 결정 방식</summary>
public enum BackgroundColorOption { White, Black, Custom }

/// <summary>CPU 점유율 정책</summary>
public enum CpuUsageOption { Max, Optimal, Half, Low, Minimum }

/// <summary>중복 파일 처리 방침</summary>
public enum OverwritePolicy { Overwrite, Suffix, Skip }
