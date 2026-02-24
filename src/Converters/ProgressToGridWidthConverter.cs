using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PixConvert.Converters;

/// <summary>
/// 0~100 사이의 숫자를 Grid의 별표(*) 너비로 변환합니다. (Progress 전용)
/// </summary>
public class ProgressToGridWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double progress)
        {
            return new GridLength(progress, GridUnitType.Star);
        }
        if (value is int intProgress)
        {
            return new GridLength((double)intProgress, GridUnitType.Star);
        }
        return new GridLength(0, GridUnitType.Star);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
