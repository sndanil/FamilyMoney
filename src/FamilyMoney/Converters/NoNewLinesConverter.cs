using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace FamilyMoney.Converters;

public class NoNewLinesConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string str)
        {
            return str.Replace(Environment.NewLine, " ");
        }

        return string.Empty;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
