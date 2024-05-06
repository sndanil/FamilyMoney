using Avalonia.Controls;
using Avalonia.Data.Converters;
using FamilyMoney.ViewModels;
using System;
using System.Globalization;

namespace FamilyMoney.Converters;

public class TransactionTypeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is TransferTransactionViewModel)
        {
            return "Перевод";
        }

        if (value is DebetTransactionViewModel)
        {
            return "Доход";
        }

        if (value is CreditTransactionViewModel)
        {
            return "Расход";
        }

        return "Транзакция";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
