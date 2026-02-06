using System.Windows;

namespace PixConvert.Services;

/// <summary>
/// 뷰모델에서 뷰(창)를 제어하기 위한 창 관리 서비스의 인터페이스입니다.
/// </summary>
public interface IWindowService
{
    /// <summary>
    /// 지정된 타입의 창을 모달(ShowDialog) 방식으로 표시합니다.
    /// </summary>
    /// <typeparam name="T">표시할 창의 클래스 타입</typeparam>
    /// <param name="viewModel">창의 DataContext로 설정할 뷰모델 객체</param>
    /// <returns>사용자의 상호작용 결과에 따른 대화 상자 결과값</returns>
    bool? ShowDialog<T>(object viewModel) where T : Window;

    /// <summary>
    /// 지정된 타입의 창을 일반 모드(Show)로 표시합니다.
    /// </summary>
    /// <typeparam name="T">표시할 창의 클래스 타입</typeparam>
    /// <param name="viewModel">창의 DataContext로 설정할 뷰모델 객체</param>
    void Show<T>(object viewModel) where T : Window;
}
