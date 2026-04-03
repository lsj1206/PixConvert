using System.Threading.Tasks;
using PixConvert.Models;

namespace PixConvert.Services.Interfaces;

/// <summary>
/// 애플리케이션 전역 설정(settings.json)을 관리하는 서비스 인터페이스입니다.
/// </summary>
public interface ISettingService
{
    /// <summary>현재 로드된 설정 객체입니다.</summary>
    AppSettings Settings { get; }

    /// <summary>설정 파일을 읽어 Settings 객체를 초기화하고 설정값을 애플리케이션에 적용합니다.</summary>
    Task InitializeAsync();

    /// <summary>설정 파일을 읽어 Settings 객체를 초기화합니다.</summary>
    Task LoadAsync();

    /// <summary>현재 Settings 객체의 상태를 설정 파일로 저장합니다. 성공 시 true, 실패 시 false를 반환합니다.</summary>
    Task<bool> SaveAsync();
}
