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
            string? desiredAlignment = parameter as string; // "Right" или "Left"

            if (isSentByMe && desiredAlignment == "Right")
                return HorizontalAlignment.Right;
            if (!isSentByMe && desiredAlignment == "Left")
                return HorizontalAlignment.Left;
            
            // По умолчанию для "своих" - справа, для "чужих" - слева
            return isSentByMe ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        }
        return HorizontalAlignment.Left; // По умолчанию
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
