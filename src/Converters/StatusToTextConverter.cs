using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using PixConvert.Models;

namespace PixConvert.Converters;

/// <summary>
/// FileConvertStatus 열거형 값을 지역화된 리소스 문자열로 변환합니다.
/// </summary>
public class StatusToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is FileConvertStatus status)
        {
            string key = $"Status_{status}";
            return Application.Current.TryFindResource(key) ?? status.ToString();
        }
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
