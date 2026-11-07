using System;
using System.Globalization;

namespace FamilyMoney.Utils;

/// <summary>Форматирование денег: как в списках приложения и компактно на осях графика.</summary>
public static class MoneyFormat
{
    /// <summary>Компактный вид для осей: «1,64 млн», «918 тыс», «120».</summary>
    public static string Compact(decimal value)
    {
        var sign = value < 0m ? "-" : string.Empty;
        var abs = Math.Abs(value);

        if (abs >= 1_000_000m)
        {
            return sign + (abs / 1_000_000m).ToString("0.##", CultureInfo.CurrentCulture) + " млн";
        }

        if (abs >= 1_000m)
        {
            return sign + (abs / 1_000m).ToString("0.#", CultureInfo.CurrentCulture) + " тыс";
        }

        return sign + abs.ToString("0", CultureInfo.CurrentCulture);
    }

    /// <summary>Полный вид как в списках: «1 643 000,00».</summary>
    public static string Full(decimal value)
    {
        return value.ToString("#,##0.00", CultureInfo.CurrentCulture);
    }

    /// <summary>То же, но с «+» у положительных значений (для изменения баланса).</summary>
    public static string Signed(decimal value)
    {
        var text = Full(value);
        return value > 0m ? "+" + text : text;
    }
}
