using System;
using System.Drawing;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace AvaloniaClient.Converters;

public class DarkenColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not Color c) return AvaloniaProperty.UnsetValue;
        
        var factor = System.Convert.ToDouble(parameter);
        
        return Color.FromArgb(
            c.A,
            (byte)(c.R * (1 - factor)),
            (byte)(c.G * (1 - factor)),
            (byte)(c.B * (1 - factor)));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}