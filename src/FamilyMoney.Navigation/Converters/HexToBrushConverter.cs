using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace FamilyMoney.Navigation.Converters;

public sealed class HexToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string text && Color.TryParse(text, out var color))
        {
            return new SolidColorBrush(color);
        }

        return Brushes.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
