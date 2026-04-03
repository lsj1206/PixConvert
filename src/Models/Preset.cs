using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PixConvert.Models;

/// <summary>
/// 이미지 변환 설정을 이름과 함께 저장하는 프리셋 클래스입니다.
/// </summary>
public partial class ConvertPreset : ObservableObject
{
    [ObservableProperty]
    private string name = "Preset";

    public ConvertSettings Settings { get; set; } = new();
}

/// <summary>
/// presets.json 파일에 저장될 전체 설정 구조입니다.
/// </summary>
public class PresetConfig
{
    /// <summary>마지막으로 사용된 프리셋의 이름 (또는 현재 상태)</summary>
    public string LastSelectedPresetName { get; set; } = string.Empty;

    /// <summary>사용자가 정의한 프리셋 목록</summary>
    public List<ConvertPreset> Presets { get; set; } = new();
}
