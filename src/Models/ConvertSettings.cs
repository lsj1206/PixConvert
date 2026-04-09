namespace PixConvert.Models;

/// <summary>
/// 이미지 변환 시 사용되는 옵션(포맷, 품질, 저장 정책 등)을 담는 데이터 모델입니다.
/// </summary>
public class ConvertSettings
{
    /// <summary>일반 이미지를 변환할 목표 확장자 (예: JPEG)</summary>
    public string StandardTargetFormat { get; set; } = "JPEG";

    /// <summary>애니메이션(움짤)을 변환할 목표 확장자 (예: GIF). 비활성화 시 null입니다.</summary>
    public string? AnimationTargetFormat { get; set; } = "GIF";

    /// <summary>CPU 작업 부하 옵션</summary>
    public CpuUsageOption CpuUsage { get; set; } = CpuUsageOption.Optimal;

    /// <summary>일반 이미지 품질 (1~100). JPEG / WEBP / AVIF에서만 의미가 있습니다.</summary>
    public int StandardQuality { get; set; } = 85;

    /// <summary>일반 이미지 무손실 인코딩 여부. WEBP / AVIF에 적용됩니다.</summary>
    public bool StandardLossless { get; set; }

    /// <summary>일반 이미지 배경 채우기 색상 (HEX, 예: #FFFFFF)</summary>
    public string StandardBackgroundColor { get; set; } = "#FFFFFF";

    /// <summary>애니메이션 이미지 품질 (1~100). 현재 WEBP에서만 의미가 있습니다.</summary>
    public int AnimationQuality { get; set; } = 85;

    /// <summary>애니메이션 이미지 무손실 인코딩 여부. 현재 WEBP에 적용됩니다.</summary>
    public bool AnimationLossless { get; set; }

    /// <summary>동일한 이름의 파일이 존재할 경우 처리 방침</summary>
    public OverwritePolicy OverwritePolicy { get; set; } = OverwritePolicy.Suffix;

    /// <summary>하위 폴더 생성 정책</summary>
    public SaveFolderMethod FolderMethod { get; set; } = SaveFolderMethod.CreateFolder;

    /// <summary>하위 폴더 이름 (필터/토큰 지원)</summary>
    public string OutputSubFolderName { get; set; } = "PixConvert";

    /// <summary>사용자 지정 출력 경로</summary>
    public string CustomOutputPath { get; set; } = string.Empty;

    /// <summary>출력 저장 위치 방식</summary>
    public SaveLocationType SaveLocation { get; set; } = SaveLocationType.SameAsOriginal;
}

/// <summary>CPU 점유율 정책</summary>
public enum CpuUsageOption { Max, Optimal, Half, Low, Minimum }

/// <summary>중복 파일 처리 방침</summary>
public enum OverwritePolicy { Overwrite, Suffix, Skip }

/// <summary>하위 폴더 생성 전략</summary>
public enum SaveFolderMethod { NoFolder, CreateFolder }

/// <summary>출력 저장 위치 유형</summary>
public enum SaveLocationType { SameAsOriginal, Custom }
