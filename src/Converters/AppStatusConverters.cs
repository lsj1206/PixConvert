using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using PixConvert.Models;

namespace PixConvert.Converters;

/// <summary>
/// AppStatus가 Idle이 아니면 Visible을, Idle이면 Collapsed를 반환합니다.
/// </summary>
public class AppStatusToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is AppStatus status)
        {
            return status != AppStatus.Idle ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// AppStatus가 Idle이면 true를, 아니면 false를 반환합니다. (명령 CanExecute용)
/// </summary>
public class AppStatusToInverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is AppStatus status)
        {
            return status == AppStatus.Idle;
        }
        return true;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
