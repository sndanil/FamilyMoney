using Avalonia.Data.Converters;
using System.Globalization;

namespace FamilyMoney.Converters;

public class IsNotEqualToConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
        {
            return true;
        }

        return !value.Equals(parameter);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
