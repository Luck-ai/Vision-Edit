using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Layout;

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

public class BoolToThicknessConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b && parameter is string p)
        {
            var parts = p.Split(',');
            if (parts.Length == 2)
            {
                return b ? new Thickness(double.Parse(parts[0])) : new Thickness(double.Parse(parts[1]));
            }
        }
        return new Thickness(0);
    }
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class BoolToAlignmentConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b && parameter is string p)
        {
            var parts = p.Split(',');
            if (parts.Length == 2)
            {
                return b ? Enum.Parse<HorizontalAlignment>(parts[0]) : Enum.Parse<HorizontalAlignment>(parts[1]);
            }
        }
        return HorizontalAlignment.Left;
    }
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class BoolToStringConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b && parameter is string p)
        {
            var parts = p.Split(',');
            if (parts.Length == 2)
            {
                return b ? parts[0] : parts[1];
            }
        }
        return string.Empty;
    }
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class BoolToArrowConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return (value is bool b && b) ? "‹" : "›";
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
