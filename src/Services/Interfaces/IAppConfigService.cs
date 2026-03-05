using System.Collections.Generic;
using System.Threading.Tasks;
using PixConvert.Models;

namespace PixConvert.Services.Interfaces;

/// <summary>
/// 애플리케이션의 설정 및 프리셋 정보를 관리하고 파일로 영속화하는 서비스 인터페이스입니다.
/// </summary>
public interface IAppConfigService
{
    /// <summary>전체 설정 데이터 (프리셋 목록 및 선택 정보 등)</summary>
    AppConfig Config { get; }

    /// <summary>설정 파일을 읽어 Config 객체를 초기화합니다.</summary>
    Task LoadAsync();

    /// <summary>현재 Config 객체의 상태를 설정 파일로 저장합니다.</summary>
    Task SaveAsync();

    /// <summary>신규 프리셋을 생성합니다.</summary>
    void AddPreset(string name, ConvertSettings settings);

    /// <summary>기존 프리셋을 제거합니다.</summary>
    void RemovePreset(string name);

    /// <summary>기존 프리셋의 이름을 변경합니다.</summary>
    void RenamePreset(string oldName, string newName);

    /// <summary>지정된 프리셋을 복사하여 새로운 프리셋을 만듭니다.</summary>
    void CopyPreset(string sourceName, string newName);
}
