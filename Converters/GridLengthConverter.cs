using Avalonia.Controls;
using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;

namespace FamilyMoney.Converters
{
    public class GridLengthConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            double val = System.Convert.ToDouble(value);
            GridLength gridLength = new GridLength(val);

            return gridLength;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null)
                return 0;

            var gridLength = (GridLength)value;

            return System.Convert.ToInt32(gridLength.Value);
        }
    }
}
