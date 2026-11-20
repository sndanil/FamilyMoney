using System;
using System.Collections.Generic;
using System.Text;

namespace FamilyMoney.Utils;

public static class IconLetter
{
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "и", "в", "во", "на", "с", "со", "для", "по", "из", "к", "ко", "о", "об",
        "от", "до", "за", "под", "над", "при", "или", "а", "но",
    };

    /// <summary>
    /// Первая буква каждого слова, но не более двух: «Дача» → Д,
    /// «Продукты питания» → ПП, «Сбербанк России» → СР.
    /// Служебные слова («Дача и огород») пропускаем.
    /// </summary>
    public static string Suggest(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var letters = new StringBuilder(2);
        foreach (var word in SplitWords(name))
        {
            if (word.Length == 0 || StopWords.Contains(word))
            {
                continue;
            }

            var first = word[0];
            if (!char.IsLetterOrDigit(first))
            {
                continue;
            }

            letters.Append(char.ToUpperInvariant(first));
            if (letters.Length == 2)
            {
                break;
            }
        }

        return letters.ToString();
    }

    public static bool TryNormalize(string? value, out string letter)
    {
        return IconSpec.TryNormalizeLetter(value, out letter);
    }

    private static IEnumerable<string> SplitWords(string name)
    {
        var builder = new StringBuilder();
        foreach (var ch in name)
        {
            if (char.IsWhiteSpace(ch) || ch is '-' or '—' or '/' or '\\' or ',' or ';' or '(' or ')')
            {
                if (builder.Length > 0)
                {
                    yield return builder.ToString();
                    builder.Clear();
                }

                continue;
            }

            // Точка внутри аббревиатуры (ДОМ.РФ) не должна давать лишнее слово.
            if (ch == '.')
            {
                continue;
            }

            builder.Append(ch);
        }

        if (builder.Length > 0)
        {
            yield return builder.ToString();
        }
    }
}
