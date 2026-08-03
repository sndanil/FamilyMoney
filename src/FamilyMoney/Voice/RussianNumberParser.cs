using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace FamilyMoney.Voice;

internal static partial class RussianNumberParser
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

    [GeneratedRegex(@"^\d{1,3}([ .]\d{3})+([.,]\d{1,2})?$")]
    private static partial Regex GroupedWithSpaceOrDotRegex();

    [GeneratedRegex(@"^(?<int>\d{1,3}(?:\.\d{3})+),(?<frac>\d{1,2})$")]
    private static partial Regex EuropeanDotThousandsCommaDecimalRegex();

    [GeneratedRegex(@"^(?<int>\d{1,3}(?:,\d{3})+)\.(?<frac>\d{1,2})$")]
    private static partial Regex UsCommaThousandsDotDecimalRegex();

    [GeneratedRegex(@"^(?<a>\d+)(?<sep>[.,])(?<b>\d+)$")]
    private static partial Regex SingleSeparatorRegex();

    public static bool TryParse(string text, out decimal value)
    {
        value = 0m;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (TryParseSpokenNumeric(text, out value))
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

    /// <summary>
    /// Разбор чисел из STT: «5.438» = 5438 (точка — разделитель тысяч),
    /// «5,75» / «5.75» = 5.75, «1.234,56» = 1234.56.
    /// </summary>
    private static bool TryParseSpokenNumeric(string text, out decimal value)
    {
        value = 0m;

        var s = text.Trim()
            .Replace('\u00A0', ' ')
            .Replace('\u202F', ' ')
            .Replace('и', ',');

        // Убрать пробелы вокруг, но сохранить внутренние как возможные тысячи.
        s = Regex.Replace(s, @"\s+", " ").Trim();

        if (string.IsNullOrEmpty(s) || s.Any(ch => !(char.IsDigit(ch) || ch is '.' or ',' or ' ')))
        {
            return false;
        }

        // 5 438 / 5.438.120 / 5 438,75 / 5.438,75
        if (GroupedWithSpaceOrDotRegex().IsMatch(s))
        {
            string intPart;
            string? fracPart = null;

            var comma = s.LastIndexOf(',');
            var lastDot = s.LastIndexOf('.');
            if (comma > 0 && comma > lastDot)
            {
                intPart = s[..comma];
                fracPart = s[(comma + 1)..];
            }
            else
            {
                intPart = s;
            }

            intPart = intPart.Replace(" ", string.Empty).Replace(".", string.Empty);
            if (!decimal.TryParse(intPart, NumberStyles.None, CultureInfo.InvariantCulture, out var whole))
            {
                return false;
            }

            if (fracPart != null)
            {
                if (!decimal.TryParse("0." + fracPart, NumberStyles.Number, CultureInfo.InvariantCulture, out var frac))
                {
                    return false;
                }

                value = whole + frac;
            }
            else
            {
                value = whole;
            }

            return true;
        }

        var european = EuropeanDotThousandsCommaDecimalRegex().Match(s);
        if (european.Success)
        {
            var intPart = european.Groups["int"].Value.Replace(".", string.Empty);
            return TryCompose(intPart, european.Groups["frac"].Value, out value);
        }

        var us = UsCommaThousandsDotDecimalRegex().Match(s);
        if (us.Success)
        {
            var intPart = us.Groups["int"].Value.Replace(",", string.Empty);
            return TryCompose(intPart, us.Groups["frac"].Value, out value);
        }

        var single = SingleSeparatorRegex().Match(s.Replace(" ", string.Empty));
        if (single.Success)
        {
            var a = single.Groups["a"].Value;
            var b = single.Groups["b"].Value;
            var sep = single.Groups["sep"].Value[0];

            // Ровно 3 цифры после единственного разделителя — типичный
            // разделитель тысяч из STT («5.438» → 5438), а не дробь.
            if (b.Length == 3)
            {
                return decimal.TryParse(a + b, NumberStyles.None, CultureInfo.InvariantCulture, out value);
            }

            // 1–2 цифры — дробная часть (копейки / десятичные).
            if (b.Length is 1 or 2)
            {
                return TryCompose(a, b, out value);
            }

            // Больше 3 цифр после точки/запятой — не считаем валидным форматом суммы.
            return false;
        }

        // Простое целое: «5438»
        var plain = s.Replace(" ", string.Empty);
        return decimal.TryParse(plain, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryCompose(string intPart, string fracPart, out decimal value)
    {
        value = 0m;
        if (!decimal.TryParse(intPart, NumberStyles.None, CultureInfo.InvariantCulture, out var whole))
        {
            return false;
        }

        if (!decimal.TryParse("0." + fracPart, NumberStyles.Number, CultureInfo.InvariantCulture, out var frac))
        {
            return false;
        }

        value = whole + frac;
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

        // Один токен вида «5.438» / «5,75»
        if (tokens.Count == 1 && TryParseSpokenNumeric(tokens[0], out value))
        {
            return true;
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

            if (TryParseSpokenNumeric(token, out var numeric))
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
