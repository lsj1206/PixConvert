using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace PixConvert.Controls;

/// <summary>
/// 필터 목록 - 자주 사용되는 문자 집합을 미리 정의
/// </summary>
public enum Filter
{
    None, // 필터 사용 안 함 (수동 설정)
    ValidSpecial, // Windows 파일명에 사용가능한 특수 문자
    InvalidSpecial, // Windows 파일명에 사용불가능한 특수 문자 (< > : " / \ | ? *)
    Digits, // 숫자 (0-9)
    Alpha, // 영문자 (대소문자)
}

public enum FilterMode
{
    Allow,
    Block
}

/// <summary>
/// 파일명 / 태그 옵션 입력을 위한 FilterTextBox
/// - Filter를 통한 간편한 필터 설정
/// - 허용 문자(AllowedCharacters) 기반 필터링
/// - 차단 문자(BlockedCharacters) 기반 제거
/// - 최대 길이 제한(FilterMaxLength)
/// </summary>
public class FilterTextBox : TextBox
{
    #region 내부 상태

    // TextChanged 재귀 호출 방지용 플래그
    private bool _isFiltering;

    // Filter 적용 중 플래그 (Filter와 수동 설정 충돌 방지)
    private bool _isApplyingFilter;

    #endregion

    #region Dependency Properties

    /// <summary>
    /// 필터
    /// 설정 시 FilterMode에 따라 AllowedCharacters 또는 BlockedCharacters에 문자 집합 적용
    /// </summary>
    public Filter Filter
    {
        get => (Filter)GetValue(FilterProperty);
        set => SetValue(FilterProperty, value);
    }

    public static readonly DependencyProperty FilterProperty =
        DependencyProperty.Register(
            nameof(Filter),
            typeof(Filter),
            typeof(FilterTextBox),
            new PropertyMetadata(Filter.None, OnFilterChanged));

    /// <summary>
    /// 필터 적용 모드 (Allow: 화이트리스트, Block: 블랙리스트)
    /// </summary>
    public FilterMode FilterMode
    {
        get => (FilterMode)GetValue(FilterModeProperty);
        set => SetValue(FilterModeProperty, value);
    }

    public static readonly DependencyProperty FilterModeProperty =
        DependencyProperty.Register(
            nameof(FilterMode),
            typeof(FilterMode),
            typeof(FilterTextBox),
            new PropertyMetadata(FilterMode.Allow, OnFilterChanged));

    /// <summary>
    /// 화이트리스트
    /// null 또는 빈 문자열이면 사용하지 않음
    /// Filter가 None이 아니고 FilterMode가 Allow면 Filter가 우선 적용됨
    /// </summary>
    public string? AllowedCharacters
    {
        get => (string?)GetValue(AllowedCharactersProperty);
        set => SetValue(AllowedCharactersProperty, value);
    }

    public static readonly DependencyProperty AllowedCharactersProperty =
        DependencyProperty.Register(
            nameof(AllowedCharacters),
            typeof(string),
            typeof(FilterTextBox),
            new PropertyMetadata(null, OnFilterPropertyChanged));

    /// <summary>
    /// 블랙리스트
    /// 여기에 포함된 문자는 무조건 제거됨
    /// Filter가 None이 아니고 FilterMode가 Block이면 Filter가 우선 적용됨
    /// </summary>
    public string? BlockedCharacters
    {
        get => (string?)GetValue(BlockedCharactersProperty);
        set => SetValue(BlockedCharactersProperty, value);
    }

    public static readonly DependencyProperty BlockedCharactersProperty =
        DependencyProperty.Register(
            nameof(BlockedCharacters),
            typeof(string),
            typeof(FilterTextBox),
            new PropertyMetadata(null, OnFilterPropertyChanged));

    /// <summary>
    /// 필터링 후 적용되는 최대 길이
    /// 0 이하이면 제한 없음
    /// </summary>
    public int FilterMaxLength
    {
        get => (int)GetValue(FilterMaxLengthProperty);
        set => SetValue(FilterMaxLengthProperty, value);
    }

    public static readonly DependencyProperty FilterMaxLengthProperty =
        DependencyProperty.Register(
            nameof(FilterMaxLength),
            typeof(int),
            typeof(FilterTextBox),
            new PropertyMetadata(0, OnFilterPropertyChanged));

    #endregion

    #region 생성자

    static FilterTextBox()
    {
        // Custom Control 필수 코드
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(FilterTextBox),
            new FrameworkPropertyMetadata(typeof(FilterTextBox)));
    }

    public FilterTextBox()
    {
        TextChanged += OnTextChanged;
    }

    #endregion

    #region Filter 처리

    /// <summary>
    /// Filter 또는 FilterMode 변경 시 호출
    /// FilterMode에 따라 AllowedCharacters 또는 BlockedCharacters에 문자 집합 설정
    /// </summary>
    private static void OnFilterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FilterTextBox textBox)
            return;

        var filter = textBox.Filter;
        var mode = textBox.FilterMode;

        // None일 경우 아무것도 하지 않음
        if (filter == Filter.None)
            return;

        // Filter 적용 중 플래그 설정 (무한 루프 방지)
        textBox._isApplyingFilter = true;

        try
        {
            // 필터에 해당하는 문자 집합 가져오기
            string? characterSet = GetCharacterSet(filter);

            if (characterSet == null)
                return;

            // 모드에 따라 적용
            if (mode == FilterMode.Allow)
            {
                textBox.AllowedCharacters = characterSet;
                textBox.BlockedCharacters = null; // 명시적으로 초기화
            }
            else // FilterMode.Block
            {
                textBox.AllowedCharacters = null; // 명시적으로 초기화
                textBox.BlockedCharacters = characterSet;
            }

            // 현재 텍스트에 새 필터 규칙 적용
            if (!string.IsNullOrEmpty(textBox.Text))
            {
                textBox.ApplyFilter();
            }
        }
        finally
        {
            textBox._isApplyingFilter = false;
        }
    }

    /// <summary>
    /// 필터에 해당하는 문자 집합 반환
    /// </summary>
    private static string? GetCharacterSet(Filter filter)
    {
        return filter switch
        {
            Filter.None => null,
            Filter.ValidSpecial => " !@#$%^&()_+-=[]{};',.~",
            Filter.InvalidSpecial => "<>:\"/\\|?*",
            Filter.Digits => "0123456789",
            Filter.Alpha => "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz",
            _ => null
        };
    }

    #endregion

    #region 이벤트 처리

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isFiltering)
            return;

        _isFiltering = true;

        // 딜레이를 사용해서 입력불가를 UX로 표현
        Dispatcher.BeginInvoke(() =>
        {
            try
            {
                ApplyFilter();
            }
            finally
            {
                _isFiltering = false;
            }
        }, System.Windows.Threading.DispatcherPriority.Background);
    }

    private static void OnFilterPropertyChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is FilterTextBox tb &&
            !tb._isApplyingFilter && // Filter 적용 중이 아닐 때만
            !string.IsNullOrEmpty(tb.Text))
        {
            tb.ApplyFilter();
        }
    }

    #endregion

    #region 필터 핵심 로직

    /// <summary>
    /// 입력 텍스트에 필터 규칙을 적용한다
    /// </summary>
    private void ApplyFilter()
    {
        int caretIndex = CaretIndex;
        string original = Text ?? string.Empty;

        var builder = new StringBuilder(original.Length);

        foreach (char c in original)
        {
            // BlockedCharacters 우선 적용
            if (!string.IsNullOrEmpty(BlockedCharacters) &&
                BlockedCharacters.Contains(c))
            {
                continue;
            }

            // AllowedCharacters 적용
            if (!string.IsNullOrEmpty(AllowedCharacters) &&
                !AllowedCharacters.Contains(c))
            {
                continue;
            }

            builder.Append(c);
        }

        string filtered = builder.ToString();

        // 길이 제한
        if (FilterMaxLength > 0 && filtered.Length > FilterMaxLength)
        {
            filtered = filtered.Substring(0, FilterMaxLength);
        }

        if (filtered != original)
        {
            Text = filtered;

            // 커서 위치 보정
            CaretIndex = caretIndex > filtered.Length
                ? filtered.Length
                : caretIndex;
        }

        // 필터링이 완료된 최종 텍스트를 즉시 ViewModel에 반영 (UpdateSourceTrigger=PropertyChanged 효과)
        // 이를 통해 포커스 이동 없이 생성 버튼을 눌러도 값이 정상 반영됨
        var binding = GetBindingExpression(TextProperty);
        binding?.UpdateSource();
    }

    #endregion
}
