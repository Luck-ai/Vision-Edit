using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace VisionEditCV.Desktop.Converters;

public class BoolToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = parameter as string ?? "default";
        if (value is bool b)
        {
            return key switch
            {
                "status" => new SolidColorBrush(Color.Parse(b ? "#4ADE80" : "#9499AB")),
                "accent" => new SolidColorBrush(Color.Parse(b ? "#4FD1FF" : "#9499AB")),
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
