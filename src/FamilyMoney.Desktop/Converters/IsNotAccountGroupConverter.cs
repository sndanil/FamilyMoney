using Avalonia.Controls;
using Avalonia.Data.Converters;
using FamilyMoney.ViewModels;
using System;
using System.Globalization;

namespace FamilyMoney.Converters;

public class IsNotAccountGroupConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is AccountViewModel accountViewModel)
        {
            return !accountViewModel.IsGroup;
        }

        return true;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
