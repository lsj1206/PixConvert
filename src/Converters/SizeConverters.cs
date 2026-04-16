using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using System.Windows.Media;

namespace PixConvert.Converters;

/// <summary>
/// 파일 크기(long)를 포맷팅된 문자열로 변환합니다.
/// </summary>
public class FileSizeConverter : IValueConverter
{
    private static readonly string[] SizeSuffixes = { "B", "KB", "MB", "GB", "TB" };

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is long bytes)
        {
            return FormatFileSize(bytes);
        }
        return "0 B";
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes <= 0) return "0 B";

        int i = (int)Math.Log(bytes, 1024);
        i = Math.Min(i, SizeSuffixes.Length - 1);

        double readable = bytes / Math.Pow(1024, i);
        return $"{readable:0.#} {SizeSuffixes[i]}";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>
/// 원본 크기와 새 크기를 비교하여 증감률 문자열 (+5.2%, 0% 등)을 반환합니다.
/// MultiBinding (0: Size, 1: OutputSize)
/// </summary>
public class SizeRatioConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length >= 2 && values[0] is long oldSize && values[1] is long newSize)
        {
            if (newSize == 0) return string.Empty;
            if (oldSize == 0) return "0%";

            double ratio = ((double)newSize - oldSize) / oldSize * 100;

            if (Math.Abs(ratio) < 0.05) return "0%"; // 아주 미세한 차이는 0%로 표시

            string sign = ratio > 0 ? "+" : "";
            return $"{sign}{ratio:0.0}%";
        }
        return string.Empty;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        targetTypes.Select(_ => Binding.DoNothing).ToArray();
}

/// <summary>
/// 증감률에 따라 적절한 상태 문자열을 반환합니다.
/// MultiBinding (0: Size, 1: OutputSize)
/// </summary>
public class SizeRatioStateConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length >= 2 && values[0] is long oldSize && values[1] is long newSize)
        {
            if (newSize == 0 || oldSize == 0) return "Success";

            double ratio = ((double)newSize - oldSize) / oldSize * 100;

            if (ratio < 0.05) return "Success";
            if (ratio <= 100) return "Warning";
            return "Error";
        }
        return "Success";
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        targetTypes.Select(_ => Binding.DoNothing).ToArray();
}
