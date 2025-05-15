namespace AvaloniaClient.Converters;

using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;


public class BooleanToMessageBackgroundConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isSentByMe)
        {
            return isSentByMe ? SolidColorBrush.Parse("#DCF8C6") : SolidColorBrush.Parse("#FFFFFF");
        }
        return Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}