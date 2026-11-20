using System;
using Material.Icons;

namespace FamilyMoney.Utils;

public enum IconSpecKind
{
    Material,
    Letter,
    Pack,
}

/// <summary>
/// Разобранное значение поля Icon: material:CartOutline, letter:П или
/// pack:banks/sber. Неизвестные значения считаются отсутствующими.
/// </summary>
public readonly record struct IconSpec(
    IconSpecKind Kind,
    MaterialIconKind Material,
    string Letter,
    string PackId,
    string AssetId)
{
    public static string ForMaterial(MaterialIconKind kind) => "material:" + kind;

    public static string ForLetter(string text)
    {
        return TryNormalizeLetter(text, out var letter) ? "letter:" + letter : string.Empty;
    }

    public static string ForPack(string packId, string assetId)
    {
        return $"pack:{packId}/{assetId}";
    }

    public static bool TryParse(string? value, out IconSpec spec)
    {
        spec = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.Trim();
        var separator = text.IndexOf(':');
        if (separator < 0)
        {
            // Совместимость с записями без префикса.
            return TryMaterial(text, out spec);
        }

        var prefix = text[..separator];
        var body = text[(separator + 1)..].Trim();

        if (prefix.Equals("material", StringComparison.OrdinalIgnoreCase))
        {
            return TryMaterial(body, out spec);
        }

        if (prefix.Equals("letter", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryNormalizeLetter(body, out var letter))
            {
                return false;
            }

            spec = new IconSpec(IconSpecKind.Letter, default, letter, string.Empty, string.Empty);
            return true;
        }

        if (prefix.Equals("pack", StringComparison.OrdinalIgnoreCase))
        {
            var slash = body.IndexOf('/');
            if (slash <= 0 || slash == body.Length - 1)
            {
                return false;
            }

            var packId = body[..slash].Trim().ToLowerInvariant();
            var assetId = body[(slash + 1)..].Trim().ToLowerInvariant();
            if (!IsPackPart(packId) || !IsPackPart(assetId))
            {
                return false;
            }

            spec = new IconSpec(IconSpecKind.Pack, default, string.Empty, packId, assetId);
            return true;
        }

        return false;
    }

    public static bool TryNormalizeLetter(string? value, out string letter)
    {
        letter = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var builder = new System.Text.StringBuilder(2);
        foreach (var ch in value.Trim())
        {
            if (char.IsWhiteSpace(ch) || ch is ':' or '/')
            {
                continue;
            }

            if (!char.IsLetterOrDigit(ch))
            {
                return false;
            }

            builder.Append(char.ToUpperInvariant(ch));
            if (builder.Length > 2)
            {
                return false;
            }
        }

        if (builder.Length == 0)
        {
            return false;
        }

        letter = builder.ToString();
        return true;
    }

    private static bool TryMaterial(string text, out IconSpec spec)
    {
        spec = default;
        if (string.IsNullOrWhiteSpace(text) || !char.IsLetter(text[0]))
        {
            return false;
        }

        return Enum.TryParse<MaterialIconKind>(text, ignoreCase: true, out var kind)
            && Enum.IsDefined(kind)
            && Assign(kind, out spec);
    }

    private static bool Assign(MaterialIconKind kind, out IconSpec spec)
    {
        spec = new IconSpec(IconSpecKind.Material, kind, string.Empty, string.Empty, string.Empty);
        return true;
    }

    private static bool IsPackPart(string value)
    {
        if (value.Length is 0 or > 64)
        {
            return false;
        }

        foreach (var ch in value)
        {
            if ((ch < 'a' || ch > 'z') && (ch < '0' || ch > '9') && ch != '-')
            {
                return false;
            }
        }

        return true;
    }
}
