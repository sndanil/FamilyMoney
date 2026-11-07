using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace FamilyMoney.Converters;

/// <summary>Полужирный шрифт для групп в списках — без отдельного стиля в шаблоне.</summary>
public class BoolToFontWeightConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? FontWeight.SemiBold : FontWeight.Normal;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
