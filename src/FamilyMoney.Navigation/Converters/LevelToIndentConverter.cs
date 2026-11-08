using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace FamilyMoney.Navigation.Converters;

public sealed class LevelToIndentConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var level = value is int number && number > 0 ? number : 0;
        return new Thickness(level * 16d, 0d, 0d, 0d);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
