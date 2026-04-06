using System.Collections.Generic;
using System.Threading.Tasks;
using PixConvert.Models;

namespace PixConvert.Services.Interfaces;

/// <summary>
/// 애플리케이션의 변환 프리셋 설정을 관리하는 서비스 인터페이스입니다.
/// </summary>
public interface IPresetService
{
    /// <summary>현재 활성화된 프리셋 정보입니다. (로드 실패 시 null)</summary>
    ConvertPreset? ActivePreset { get; }

    /// <summary>현재 로드된 설정 객체입니다.</summary>
    PresetConfig Config { get; }

    /// <summary>앱 시작 시 프리셋 파일을 읽어 비동기적으로 초기화합니다.</summary>
    Task InitializeAsync();

    /// <summary>현재 Config 객체의 상태를 설정 파일로 저장합니다. 성공 시 true, 실패 시 false를 반환합니다.</summary>
    Task<bool> SaveAsync();

    /// <summary>전달받은 설정을 검증합니다.</summary>
    bool ValidPresetData(ConvertSettings settings, out string errorMessageKey);

    /// <summary>활성 프리셋을 갱신합니다.</summary>
    void UpdateActivePreset(ConvertPreset preset);

    /// <summary>신규 프리셋을 생성합니다.</summary>
    void AddPreset(string name, ConvertSettings settings);

    /// <summary>기존 프리셋을 제거합니다.</summary>
    void RemovePreset(string name);

    /// <summary>기존 프리셋의 이름을 변경합니다.</summary>
    void RenamePreset(string oldName, string newName);

    /// <summary>지정된 프리셋을 복사하여 새로운 프리셋을 만듭니다.</summary>
    void CopyPreset(string sourceName, string newName);
}
