using System;
using System.Collections.Generic;
using System.Linq;
using Material.Icons;

namespace FamilyMoney.Utils;

/// <summary>
/// Полный список Material Icons. Курируемый <see cref="IconCatalog"/> — для
/// быстрого выбора, а этот каталог нужен, когда нужной иконки там нет.
/// </summary>
public static class MaterialIconCatalog
{
    public const int PageSize = 300;

    public static IReadOnlyList<IconChoice> All { get; } = Build();

    public static IReadOnlyList<IconChoice> Search(string? query, int limit, out int total)
    {
        if (limit <= 0)
        {
            limit = PageSize;
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            total = All.Count;
            return All.Take(limit).ToList();
        }

        var text = query.Trim();
        var matches = new List<IconChoice>(limit);
        var found = 0;
        foreach (var choice in All)
        {
            if (!choice.Label.Contains(text, StringComparison.OrdinalIgnoreCase)
                && !choice.Spec.Contains(text, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            found++;
            if (matches.Count < limit)
            {
                matches.Add(choice);
            }
        }

        total = found;
        return matches;
    }

    private static IReadOnlyList<IconChoice> Build()
    {
        var result = new List<IconChoice>();
        foreach (var name in Enum.GetNames<MaterialIconKind>())
        {
            // Alias names are intentionally kept: Material.Icons exposes more
            // names than distinct enum values, and users search by those names.
            result.Add(new IconChoice("material:" + name, name, [name]));
        }

        result.Sort((left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.Label, right.Label));
        return result;
    }
}
