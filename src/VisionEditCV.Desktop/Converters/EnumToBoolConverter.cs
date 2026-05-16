using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace VisionEditCV.Desktop.Converters;

public class EnumToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null) return false;
        
        var current = value.ToString();
        var targetStr = parameter.ToString()!;
        
        if (targetStr.Contains(','))
        {
            var parts = targetStr.Split(',', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                if (current == part.Trim()) return true;
            }
            return false;
        }
        
        return current == targetStr;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b && b && parameter != null)
        {
            if (targetType.IsEnum)
                return Enum.Parse(targetType, parameter.ToString()!);
            
            // Support String comparison too
            if (targetType == typeof(string))
                return parameter.ToString();
        }
        return AvaloniaProperty.UnsetValue;
    }
}
