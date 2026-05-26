using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace VisionEditCV.Shared.Converters;

public class BoolToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = parameter as string ?? "default";
        if (value is bool b)
        {
            return key switch
            {
                "status" => new SolidColorBrush(Color.Parse(b ? "#34D399" : "#8993B5")),
                "accent" => new SolidColorBrush(Color.Parse(b ? "#22D3EE" : "#8993B5")),
                _ => b ? Brushes.LimeGreen : Brushes.Crimson
            };
        }

        return new SolidColorBrush(Color.Parse("#9499AB"));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
