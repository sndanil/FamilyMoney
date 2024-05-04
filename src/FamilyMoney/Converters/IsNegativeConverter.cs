using Avalonia.Controls;
using Avalonia.Data.Converters;
using FamilyMoney.ViewModels;
using System;
using System.Globalization;

namespace FamilyMoney.Converters;

public class IsNegativeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is decimal number)
        {
            return number < 0;
        }

        return true;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
