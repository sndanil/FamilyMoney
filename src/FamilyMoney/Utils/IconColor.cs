using System;

namespace FamilyMoney.Utils;

public static class IconColor
{
    public const string Accent = "#3390EC";

    /// <summary>#RRGGBB, регистр не важен. null/пусто — акцент.</summary>
    public static bool TryParse(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.Trim();
        if (!text.StartsWith('#'))
        {
            text = "#" + text;
        }

        if (text.Length != 7)
        {
            return false;
        }

        for (var i = 1; i < text.Length; i++)
        {
            if (!Uri.IsHexDigit(text[i]))
            {
                return false;
            }
        }

        normalized = text.ToUpperInvariant();
        return true;
    }

    public static string NormalizeOrAccent(string? value)
    {
        return TryParse(value, out var normalized) ? normalized : Accent;
    }

    public static bool IsValid(string? value) => TryParse(value, out _);
}

public static class IconPalette
{
    /// <summary>Все цвета проверены на контраст ≥ 3:1 с белым глифом и светлой/тёмной карточкой.</summary>
    public static readonly string[] Colors =
    [
        "#3390EC",
        "#2E7D32",
        "#EF6C00",
        "#E53935",
        "#AB47BC",
        "#00897B",
        "#8D6E63",
        "#78909C",
        "#D81B60",
        "#5C6BC0",
    ];
}
