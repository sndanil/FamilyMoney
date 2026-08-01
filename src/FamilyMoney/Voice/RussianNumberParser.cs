using System.Globalization;
using System.Text;

namespace FamilyMoney.Voice;

internal static class RussianNumberParser
{
    private static readonly Dictionary<string, decimal> Words = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ноль"] = 0,
        ["один"] = 1,
        ["одна"] = 1,
        ["два"] = 2,
        ["две"] = 2,
        ["три"] = 3,
        ["четыре"] = 4,
        ["пять"] = 5,
        ["шесть"] = 6,
        ["семь"] = 7,
        ["восемь"] = 8,
        ["девять"] = 9,
        ["десять"] = 10,
        ["одиннадцать"] = 11,
        ["двенадцать"] = 12,
        ["тринадцать"] = 13,
        ["четырнадцать"] = 14,
        ["пятнадцать"] = 15,
        ["шестнадцать"] = 16,
        ["семнадцать"] = 17,
        ["восемнадцать"] = 18,
        ["девятнадцать"] = 19,
        ["двадцать"] = 20,
        ["тридцать"] = 30,
        ["сорок"] = 40,
        ["пятьдесят"] = 50,
        ["шестьдесят"] = 60,
        ["семьдесят"] = 70,
        ["восемьдесят"] = 80,
        ["девяносто"] = 90,
        ["сто"] = 100,
        ["двести"] = 200,
        ["триста"] = 300,
        ["четыреста"] = 400,
        ["пятьсот"] = 500,
        ["шестьсот"] = 600,
        ["семьсот"] = 700,
        ["восемьсот"] = 800,
        ["девятьсот"] = 900,
        ["тысяча"] = 1000,
        ["тысячи"] = 1000,
    };

    public static bool TryParse(string text, out decimal value)
    {
        value = 0m;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = text.Trim()
            .Replace('б', CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator[0])
            .Replace(',', '.');

        if (decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out value)
            || decimal.TryParse(text.Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out value))
        {
            return true;
        }

        var tokens = SplitTokens(text);
        if (tokens.Count == 0)
        {
            return false;
        }

        decimal total = 0;
        decimal current = 0;
        var matched = false;

        foreach (var token in tokens)
        {
            if (decimal.TryParse(token.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var numeric))
            {
                current += numeric;
                matched = true;
                continue;
            }

            if (!Words.TryGetValue(token, out var wordValue))
            {
                return false;
            }

            matched = true;
            if (wordValue == 1000)
            {
                if (current == 0)
                {
                    current = 1;
                }

                total += current * 1000;
                current = 0;
            }
            else if (wordValue >= 100)
            {
                current += wordValue;
            }
            else
            {
                current += wordValue;
            }
        }

        if (!matched)
        {
            return false;
        }

        value = total + current;
        return true;
    }

    private static List<string> SplitTokens(string text)
    {
        var result = new List<string>();
        var current = new StringBuilder();

        foreach (var ch in text.ToLowerInvariant().Replace('ё', 'е'))
        {
            if (char.IsLetterOrDigit(ch) || ch is '.' or ',')
            {
                current.Append(ch);
            }
            else if (current.Length > 0)
            {
                result.Add(current.ToString());
                current.Clear();
            }
        }

        if (current.Length > 0)
        {
            result.Add(current.ToString());
        }

        return result;
    }
}
