using System.Collections.Generic;

namespace FamilyMoney.ViewModels;

/// <summary>
/// Плоский список счетов для выбора в разделе «Отчёты»: «Всего», группы и счета с иконками.
/// <para>
/// Скрытые счета в список не попадают — это только фильтр отображения. На расчёт они
/// не влияют: при выборе «Всего» или группы скрытые счета учитываются как обычно,
/// потому что <c>TimelineScopeResolver</c> работает с моделями счетов, а не с этим списком.
/// </para>
/// </summary>
public static class TimelineScopeCatalog
{
    public static IReadOnlyList<TimelineScope> Build(AccountViewModel total)
    {
        var result = new List<TimelineScope>
        {
            new(null, total.Name, 0, false, null),
        };

        foreach (var account in total.Children)
        {
            if (account.IsHidden)
            {
                // Скрытая группа скрывает и своё содержимое.
                continue;
            }

            result.Add(new TimelineScope(account.Id, account.Name, 0, account.IsGroup, account.ImageData));

            foreach (var child in account.Children)
            {
                if (child.IsHidden)
                {
                    continue;
                }

                result.Add(new TimelineScope(child.Id, child.Name, 1, child.IsGroup, child.ImageData));
            }
        }

        return result;
    }
}
