using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace FamilyMoney.Navigation.Converters;

public sealed class DateTimeToDateTimeOffsetConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is DateTime date
            ? new DateTimeOffset(DateTime.SpecifyKind(date, DateTimeKind.Local))
            : null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is DateTimeOffset date
            ? date.LocalDateTime
            : null;
    }
}
