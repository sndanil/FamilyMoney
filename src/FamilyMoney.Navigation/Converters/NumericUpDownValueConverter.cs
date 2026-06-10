using Avalonia.Data.Converters;
using System.Globalization;

namespace FamilyMoney.Navigation.Converters;

public sealed class NumericUpDownValueConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return default(decimal);
        }

        return (decimal?)((IConvertible)value).ToDecimal(culture);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return 0m;
        }

        return ((IConvertible)value).ToDecimal(culture);
    }
}
