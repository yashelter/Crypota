namespace AvaloniaClient.Converters;

using Avalonia.Data.Converters;
using Avalonia.Layout;
using System;
using System.Globalization;

public class BooleanToAlignmentConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isSentByMe)
        {
            string? desiredAlignment = parameter as string;

            if (isSentByMe && desiredAlignment == "Right")
                return HorizontalAlignment.Right;
            if (!isSentByMe && desiredAlignment == "Left")
                return HorizontalAlignment.Left;
            
            return isSentByMe ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        }
        return HorizontalAlignment.Left;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
