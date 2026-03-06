using System.Collections.Generic;
using System.Threading.Tasks;
using PixConvert.Models;

namespace PixConvert.Services.Interfaces;

/// <summary>
/// 애플리케이션의 변환 프리셋 설정을 관리하는 서비스 인터페이스입니다.
/// </summary>
public interface IPresetService
{
    /// <summary>현재 로드된 설정 객체입니다.</summary>
    PresetConfig Config { get; }

    /// <summary>설정 파일을 읽어 Config 객체를 초기화합니다.</summary>
    Task LoadAsync();

    /// <summary>현재 Config 객체의 상태를 설정 파일로 저장합니다.</summary>
    Task SaveAsync();

    /// <summary>현재 구조의 무결성을 검사하고 이상 시 기본값으로 복구합니다. 정상이면 true를 반환합니다.</summary>
    bool ValidPresetFile();

    /// <summary>현재 선택된 프리셋의 설정 값들을 검증합니다.</summary>
    bool ValidPresetData(out string errorMessageKey);


    /// <summary>신규 프리셋을 생성합니다.</summary>
    void AddPreset(string name, ConvertSettings settings);

    /// <summary>기존 프리셋을 제거합니다.</summary>
    void RemovePreset(string name);

    /// <summary>기존 프리셋의 이름을 변경합니다.</summary>
    void RenamePreset(string oldName, string newName);

    /// <summary>지정된 프리셋을 복사하여 새로운 프리셋을 만듭니다.</summary>
    void CopyPreset(string sourceName, string newName);
}
