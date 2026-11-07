using FamilyMoney.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FamilyMoney.Analytics;

/// <summary>
/// Определяет, из каких счетов складывается выбранная область: «Всего», группа или один счёт.
/// Правило <see cref="Account.IsNotSummable"/> соблюдается так же, как в левой панели:
/// счёт с этим флагом не входит в сумму родителя, но считается, если выбран сам.
/// </summary>
public static class TimelineScopeResolver
{
    public static IReadOnlyCollection<Guid> Resolve(IEnumerable<Account> accounts, Guid? rootAccountId)
    {
        ArgumentNullException.ThrowIfNull(accounts);

        var alive = accounts.Where(a => a.DeletedAt == null).ToList();
        var byParent = alive
            .Where(a => a.ParentId != null)
            .ToLookup(a => a.ParentId!.Value);

        var result = new HashSet<Guid>();

        if (rootAccountId is null)
        {
            foreach (var root in alive.Where(a => a.ParentId == null).OrderBy(a => a.Order))
            {
                Walk(root, excluded: false);
            }
        }
        else
        {
            var root = alive.FirstOrDefault(a => a.Id == rootAccountId.Value);
            if (root != null)
            {
                Walk(root, excluded: false);
            }
        }

        return result;

        void Walk(Account account, bool excluded)
        {
            if (!account.IsGroup)
            {
                if (!excluded)
                {
                    result.Add(account.Id);
                }

                return;
            }

            foreach (var child in byParent[account.Id].OrderBy(a => a.Order))
            {
                // Флаг самого корня не применяем: выбранный счёт считается всегда.
                Walk(child, excluded || child.IsNotSummable);
            }
        }
    }
}
