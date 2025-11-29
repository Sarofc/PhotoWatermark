using Avalonia.Data.Converters;
using System.Globalization;
using System;

namespace PhotoWatermark.Converters;

public class ExposureTimeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double d)
        {
            return FormatExposureTime(d);
        }
        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    public static string FormatExposureTime(double exposureTime)
    {
        if (exposureTime >= 1.0)
        {
            // 长于1秒的曝光
            return $"{exposureTime:0.##}";
        }
        else
        {
            // 短于1秒的曝光，转换为分数形式
            double denominator = 1.0 / exposureTime;
            return $"1/{Math.Round(denominator):0}";
        }
    }
}