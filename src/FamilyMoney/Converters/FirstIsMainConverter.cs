using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace FamilyMoney.Converters;

public sealed class FirstIsMainConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        // Ensure all bindings are provided and attached to correct target type
        if (values?.Any() == true)
        {
            var first = values[0];
            if (first is bool b && b)
            {
                return values.Skip(1).OfType<bool>().Any(v => v);
            }
        }

        return false;
    }
}
