using System;
using System.Collections.Generic;
using System.Linq;

namespace FamilyMoney.DataAccess;

/// <summary>Одна транзакция-кандидат для подсказки комментария.</summary>
public readonly record struct CommentCandidate(DateTime Date, DateTime LastChange, string Comment);

/// <summary>
/// Собирает подсказки комментариев для пары «категория + подкатегория»:
/// последние уникальные и самые популярные. Всего не больше <see cref="DefaultTotalLimit"/>.
/// </summary>
public static class CommentSuggestionBuilder
{
    public const int DefaultRecentLimit = 10;

    public const int DefaultTotalLimit = 20;

    public static IReadOnlyList<string> Build(
        IEnumerable<CommentCandidate> candidates,
        int recentLimit = DefaultRecentLimit,
        int totalLimit = DefaultTotalLimit)
    {
        totalLimit = Math.Max(0, totalLimit);
        recentLimit = Math.Clamp(recentLimit, 0, totalLimit);

        var normalized = candidates
            .Select(c => new CommentCandidate(c.Date, c.LastChange, c.Comment.Trim()))
            .Where(c => c.Comment.Length > 0)
            .ToList();

        if (normalized.Count == 0 || totalLimit == 0)
        {
            return [];
        }

        var recent = normalized
            .OrderByDescending(c => c.Date)
            .ThenByDescending(c => c.LastChange)
            .ThenBy(c => c.Comment, StringComparer.OrdinalIgnoreCase)
            .Select(c => c.Comment)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(recentLimit)
            .ToList();

        var popular = normalized
            .GroupBy(c => c.Comment, StringComparer.OrdinalIgnoreCase)
            .Select(g => new
            {
                // Показываем самый свежий вариант написания.
                Comment = g.OrderByDescending(c => c.Date)
                    .ThenByDescending(c => c.LastChange)
                    .Select(c => c.Comment)
                    .First(),
                Count = g.Count(),
                LastDate = g.Max(c => c.Date),
                LastChange = g.Max(c => c.LastChange),
            })
            .Where(p => !recent.Contains(p.Comment, StringComparer.OrdinalIgnoreCase))
            .OrderByDescending(p => p.Count)
            .ThenByDescending(p => p.LastDate)
            .ThenByDescending(p => p.LastChange)
            .ThenBy(p => p.Comment, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(0, totalLimit - recent.Count))
            .Select(p => p.Comment);

        return recent.Concat(popular).ToList();
    }
}
