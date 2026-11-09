using System.Globalization;

namespace FamilyMoney.ViewModels;

public enum DepositTermKind
{
    None,
    Calm,
    Soon,
    Expired,
}

public readonly record struct DepositCaption(string? Text, DepositTermKind Kind)
{
    public bool HasText => !string.IsNullOrEmpty(Text);
}

public static class DepositCaptionFormatter
{
    public static DepositCaption Format(DateTime? endsOn, decimal? annualRate, int warningDays, DateTime today)
    {
        var rateText = annualRate is { } rate ? FormatRate(rate) : null;
        if (endsOn is not { } ends)
        {
            return new DepositCaption(rateText, DepositTermKind.None);
        }

        var date = ends.Date;
        var days = (date - today.Date).Days;
        var dateText = date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
        var threshold = Math.Max(0, warningDays);
        var kind = days < 0
            ? DepositTermKind.Expired
            : days <= threshold
                ? DepositTermKind.Soon
                : DepositTermKind.Calm;

        var parts = new List<string>
        {
            kind == DepositTermKind.Expired ? $"истёк {dateText}" : $"до {dateText}",
        };

        if (kind == DepositTermKind.Soon)
        {
            parts.Add($"{days} дн.");
        }

        if (rateText != null)
        {
            parts.Add(rateText);
        }

        return new DepositCaption(string.Join(" · ", parts), kind);
    }

    public static string FormatRate(decimal rate)
    {
        return rate.ToString("0.##", CultureInfo.CurrentCulture) + "%";
    }

    public static string FormatRateInput(decimal? rate)
    {
        return rate is { } value
            ? value.ToString("0.##", CultureInfo.CurrentCulture)
            : string.Empty;
    }

    public static bool TryParseRate(string? text, out decimal? rate)
    {
        rate = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        var normalized = text.Trim().TrimEnd('%').Trim()
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("\u00A0", string.Empty, StringComparison.Ordinal)
            .Replace(',', '.');

        if (normalized.Count(character => character == '.') > 1)
        {
            return false;
        }

        if (!decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)
            || value < 0)
        {
            return false;
        }

        rate = decimal.Round(value, 2, MidpointRounding.AwayFromZero);
        return true;
    }
}
