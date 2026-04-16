using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using PixConvert.Models;

namespace PixConvert.Converters;

/// <summary>
/// 문자열을 대문자로 변환합니다.
/// </summary>
public class UpperCaseConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value?.ToString()?.ToUpper() ?? string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

/// <summary>
/// Enum 값이 매개변수와 일치하면 true를 반환합니다.
/// </summary>
public class EnumToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null) return false;
        return value.ToString() == parameter.ToString();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b && b && parameter != null)
        {
            return Enum.Parse(targetType, parameter.ToString()!);
        }
        return Binding.DoNothing;
    }
}

/// <summary>
/// AppStatus가 ConverterParameter로 전달된 상태 문자열(파이프(|) 구분 지원) 중 하나와 일치하면 Visible, 아니면 Collapsed를 반환합니다.
/// 예: ConverterParameter="Converting|Processing"
/// 반전(Collapsed 반환)이 필요한 경우 ConverterParameter 시작을 "!"로 지정하면 일치할 때 Collapsed를 반환합니다.
/// 예: ConverterParameter="!Converting"
/// </summary>
public class MultiStatusVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is AppStatus status && parameter is string paramString && !string.IsNullOrWhiteSpace(paramString))
        {
            bool inverse = paramString.StartsWith("!");
            if (inverse) paramString = paramString.Substring(1);

            string[] statuses = paramString.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            bool match = false;

            foreach (var s in statuses)
            {
                if (Enum.TryParse<AppStatus>(s.Trim(), true, out var parsedStatus))
                {
                    if (status == parsedStatus)
                    {
                        match = true;
                        break;
                    }
                }
            }

            if (inverse)
                return match ? Visibility.Collapsed : Visibility.Visible;
            else
                return match ? Visibility.Visible : Visibility.Collapsed;
        }

        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}
