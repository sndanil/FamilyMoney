using FamilyMoney.Utils;

namespace FamilyMoney.Voice;

internal static class VoiceNameMatcher
{
    public static T? FindBest<T>(IEnumerable<T> items, Func<T, string?> nameSelector, string query)
        where T : class
    {
        var normalizedQuery = Normalize(query);
        if (string.IsNullOrEmpty(normalizedQuery))
        {
            return null;
        }

        var translatedQuery = Normalize(KeyboardHelper.Translate(query));

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
            var score = Score(normalizedName, normalizedQuery, translatedQuery);
            if (score > bestScore)
            {
                bestScore = score;
                best = item;
            }
        }

        // Require at least a substring match.
        return bestScore >= 20 ? best : null;
    }

    private static int Score(string name, string query, string translatedQuery)
    {
        if (name.Equals(query, StringComparison.Ordinal) || name.Equals(translatedQuery, StringComparison.Ordinal))
        {
            return 100;
        }

        if (name.StartsWith(query, StringComparison.Ordinal) || name.StartsWith(translatedQuery, StringComparison.Ordinal))
        {
            return 80;
        }

        if (name.Contains(query, StringComparison.Ordinal) || name.Contains(translatedQuery, StringComparison.Ordinal))
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

        return value.Trim().ToLowerInvariant().Replace('ё', 'е');
    }
}
