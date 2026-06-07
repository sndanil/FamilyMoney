using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace FamilyMoney.Converters;

public class IsNotZeroConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is decimal number)
        {
            return number != 0;
        }

        return true;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
