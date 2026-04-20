using PixConvert.Models;

namespace PixConvert.Services.Interfaces;

/// <summary>
/// 파일 특성 및 설정에 맞는 변환 provider를 선택합니다.
/// </summary>
public interface IEngineSelector
{
    IProviderService GetProvider(FileItem file, ConvertSettings settings);
}
