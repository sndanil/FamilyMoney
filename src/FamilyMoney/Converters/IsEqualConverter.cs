using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace FamilyMoney.Converters;

public class IsEqualConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.All(v => v is not null))
        {
            for (int i = 1; i < values.Count; i++)
            {
                if (!object.ReferenceEquals(values[i - 1], values[i]))
                    return false;
            }

            return true;
        }

        return false;
    }
}
