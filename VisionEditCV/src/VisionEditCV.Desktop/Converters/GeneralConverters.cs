using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace VisionEditCV.Desktop.Converters;

public class BoolToDoubleConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b && parameter is string p)
        {
            var parts = p.Split(',');
            if (parts.Length == 2)
            {
                return b ? double.Parse(parts[0]) : double.Parse(parts[1]);
            }
        }
        return 0.0;
    }
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class CollectionCountToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hasItems = value switch
        {
            int count => count > 0,
            System.Collections.ICollection collection => collection.Count > 0,
            _ => false
        };

        return string.Equals(parameter as string, "invert", StringComparison.OrdinalIgnoreCase)
            ? !hasItems
            : hasItems;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class IntEqualsConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int index && int.TryParse(parameter?.ToString(), out var target))
            return index == target;
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isChecked && isChecked && int.TryParse(parameter?.ToString(), out var target))
            return target;
        return AvaloniaProperty.UnsetValue;
    }
}

public class InverseBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool b ? !b : AvaloniaProperty.UnsetValue;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool b ? !b : AvaloniaProperty.UnsetValue;
    }
}
