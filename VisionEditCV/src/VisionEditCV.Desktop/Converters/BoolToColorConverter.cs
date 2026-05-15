using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace VisionEditCV.Desktop.Converters;

public class BoolToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return b ? Brushes.LimeGreen : Brushes.Crimson;
        }
        return Brushes.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
