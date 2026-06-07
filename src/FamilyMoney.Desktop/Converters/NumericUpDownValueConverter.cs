using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace FamilyMoney.Converters;

public class NumericUpDownValueConverter: IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
            return default(decimal);
        return (decimal?)((IConvertible)value).ToDecimal(culture);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
        {
            if (targetType == typeof(decimal))
                return 0m;

            throw new ArgumentNullException($"Unsupported type: {targetType.FullName}");
        }

        if (targetType == typeof(decimal))
            return ((IConvertible)value).ToDecimal(culture);

        throw new ArgumentNullException($"Unsupported type: {targetType.FullName}");
    }
}
