using System.Globalization;
using System.Text;

namespace FamilyMoney.Voice;

internal static class RussianNumberParser
{
    private static readonly HashSet<string> RubleUnits = new(StringComparer.OrdinalIgnoreCase)
    {
        "рубль", "рубля", "рублей", "руб",
    };

    private static readonly HashSet<string> KopeckUnits = new(StringComparer.OrdinalIgnoreCase)
    {
        "копейка", "копейки", "копеек", "коп",
    };

    private static readonly HashSet<string> IgnoredWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "и",
    };

    private static readonly Dictionary<string, decimal> Words = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ноль"] = 0,
        ["один"] = 1,
        ["одна"] = 1,
        ["одно"] = 1,
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
        ["тысяч"] = 1000,
    };

    public static bool TryParse(string text, out decimal value)
    {
        value = 0m;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = text.Trim()
            .Replace('и', CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator[0])
            .Replace(" ", string.Empty)
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

        var rubleIdx = tokens.FindIndex(IsRubleUnit);
        var kopeckIdx = tokens.FindIndex(IsKopeckUnit);

        if (rubleIdx < 0 && kopeckIdx < 0)
        {
            return TryParseNumberTokens(tokens, out value);
        }

        decimal rubles = 0m;
        decimal kopecks = 0m;
        var hasRubles = false;
        var hasKopecks = false;

        if (rubleIdx >= 0)
        {
            var rubleTokens = TakeNumberTokens(tokens, 0, rubleIdx);
            if (rubleTokens.Count == 0)
            {
                return false;
            }

            if (!TryParseNumberTokens(rubleTokens, out rubles))
            {
                return false;
            }

            hasRubles = true;
        }

        if (kopeckIdx >= 0)
        {
            var start = rubleIdx >= 0 ? rubleIdx + 1 : 0;
            if (kopeckIdx < start)
            {
                return false;
            }

            var kopeckTokens = TakeNumberTokens(tokens, start, kopeckIdx);
            if (kopeckTokens.Count == 0)
            {
                return false;
            }

            if (!TryParseNumberTokens(kopeckTokens, out kopecks))
            {
                return false;
            }

            hasKopecks = true;
        }
        else if (rubleIdx >= 0 && rubleIdx < tokens.Count - 1)
        {
            // «150 рублей 75» без слова «копеек»
            var trailing = TakeNumberTokens(tokens, rubleIdx + 1, tokens.Count);
            if (trailing.Count > 0)
            {
                if (!TryParseNumberTokens(trailing, out kopecks))
                {
                    return false;
                }

                hasKopecks = true;
            }
        }

        if (!hasRubles && !hasKopecks)
        {
            return false;
        }

        if (kopecks is < 0 or >= 100)
        {
            return false;
        }

        value = rubles + (kopecks / 100m);
        return true;
    }

    private static List<string> TakeNumberTokens(IReadOnlyList<string> tokens, int start, int endExclusive)
    {
        var result = new List<string>();
        for (var i = start; i < endExclusive && i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (IsCurrencyUnit(token) || IgnoredWords.Contains(token))
            {
                continue;
            }

            result.Add(token);
        }

        return result;
    }

    private static bool TryParseNumberTokens(IReadOnlyList<string> tokens, out decimal value)
    {
        value = 0m;
        if (tokens.Count == 0)
        {
            return false;
        }

        decimal total = 0;
        decimal current = 0;
        var matched = false;

        foreach (var token in tokens)
        {
            if (IgnoredWords.Contains(token) || IsCurrencyUnit(token))
            {
                continue;
            }

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

    private static bool IsRubleUnit(string token) => RubleUnits.Contains(token);

    private static bool IsKopeckUnit(string token) => KopeckUnits.Contains(token);

    private static bool IsCurrencyUnit(string token) => IsRubleUnit(token) || IsKopeckUnit(token);

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
