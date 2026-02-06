using System.Windows.Media;

namespace PixConvert.Services;

/// <summary>
/// 파일 확장자에 따른 시스템 아이콘을 추출하고 관리하는 서비스 인터페이스입니다.
/// </summary>
public interface IIconService
{
    /// <summary>
    /// 파일 경로의 확장자를 분석하여 시스템에 등록된 원본 아이콘을 반환합니다.
    /// </summary>
    /// <param name="path">파일 또는 폴더 경로</param>
    /// <returns>WPF에서 사용 가능한 ImageSource 객체</returns>
    ImageSource GetIcon(string path);
}
