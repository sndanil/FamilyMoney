using System.Text;
using System.Text.RegularExpressions;
using FamilyMoney.Utils;

namespace FamilyMoney.Voice;

internal static partial class VoiceNameMatcher
{
    [GeneratedRegex(@"\s+")]
    private static partial Regex MultiSpaceRegex();

    public static T? FindBest<T>(IEnumerable<T> items, Func<T, string?> nameSelector, string query)
        where T : class
    {
        var normalizedQuery = Normalize(query);
        if (string.IsNullOrEmpty(normalizedQuery))
        {
            return null;
        }

        var translatedQuery = Normalize(KeyboardHelper.Translate(query));
        var compactQuery = Compact(normalizedQuery);
        var compactTranslated = Compact(translatedQuery);

        T? best = null;
        var bestScore = 0;

        foreach (var item in items)
        {
            var name = nameSelector(item);
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var normalizedName = Normalize(name);
            var score = Score(normalizedName, Compact(normalizedName), normalizedQuery, translatedQuery, compactQuery, compactTranslated);
            if (score > bestScore)
            {
                bestScore = score;
                best = item;
            }
        }

        // Require at least a substring match.
        return bestScore >= 20 ? best : null;
    }

    private static int Score(
        string name,
        string compactName,
        string query,
        string translatedQuery,
        string compactQuery,
        string compactTranslated)
    {
        if (name.Equals(query, StringComparison.Ordinal)
            || name.Equals(translatedQuery, StringComparison.Ordinal)
            || compactName.Equals(compactQuery, StringComparison.Ordinal)
            || compactName.Equals(compactTranslated, StringComparison.Ordinal))
        {
            return 100;
        }

        if (name.StartsWith(query, StringComparison.Ordinal)
            || name.StartsWith(translatedQuery, StringComparison.Ordinal)
            || compactName.StartsWith(compactQuery, StringComparison.Ordinal)
            || compactName.StartsWith(compactTranslated, StringComparison.Ordinal))
        {
            return 80;
        }

        if (name.Contains(query, StringComparison.Ordinal)
            || name.Contains(translatedQuery, StringComparison.Ordinal)
            || compactName.Contains(compactQuery, StringComparison.Ordinal)
            || compactName.Contains(compactTranslated, StringComparison.Ordinal))
        {
            return 40 + Math.Min(query.Length, 20);
        }

        return 0;
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(value.Length);
        foreach (var ch in value.Trim().ToLowerInvariant())
        {
            var c = ch == 'ё' ? 'е' : ch;
            if (c is '-' or '‐' or '‑' or '‒' or '–' or '—' or '−' or '_' or '/')
            {
                sb.Append(' ');
                continue;
            }

            sb.Append(c);
        }

        return MultiSpaceRegex().Replace(sb.ToString(), " ").Trim();
    }

    private static string Compact(string value) =>
        value.Replace(" ", string.Empty, StringComparison.Ordinal);
}
