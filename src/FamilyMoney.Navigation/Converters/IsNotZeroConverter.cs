using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace FamilyMoney.Navigation.Converters;

/// <summary>Истина, если число не равно нулю.</summary>
public sealed class IsNotZeroConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            decimal number => number != 0m,
            double number => number != 0d,
            int number => number != 0,
            _ => false,
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
