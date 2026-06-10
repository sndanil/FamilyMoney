using Avalonia.Data.Converters;
using FamilyMoney.ViewModels;
using System.Globalization;

namespace FamilyMoney.Navigation.Converters;

public sealed class IsNotAccountGroupConverter : IValueConverter
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
        throw new NotSupportedException();
    }
}
