using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace FamilyMoney.Converters;

/// <summary>Превращает признак раскрытия в угол поворота шеврона.</summary>
public sealed class BoolToAngleConverter : IValueConverter
{
    public double TrueAngle { get; set; } = 90;

    public double FalseAngle { get; set; }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? TrueAngle : FalseAngle;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
