using System.Collections.Generic;

namespace FamilyMoney.ViewModels;

/// <summary>
/// Плоский список счетов для выбора в отчётах: «Всего», группы и счета с иконками.
/// <para>
/// Скрытые счета в список не попадают — это только фильтр отображения. На расчёт они
/// не влияют: при выборе «Всего» или группы скрытые счета учитываются как обычно,
/// потому что <c>ReportScopeResolver</c> работает с моделями счетов, а не с этим списком.
/// </para>
/// </summary>
public static class ReportAccountCatalog
{
    public static IReadOnlyList<ReportAccount> Build(AccountViewModel total)
    {
        var result = new List<ReportAccount>
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

            result.Add(new ReportAccount(account.Id, account.Name, 0, account.IsGroup, account.ImageData, account.Icon, account.IconColor));

            foreach (var child in account.Children)
            {
                if (child.IsHidden)
                {
                    continue;
                }

                result.Add(new ReportAccount(child.Id, child.Name, 1, child.IsGroup, child.ImageData, child.Icon, child.IconColor));
            }
        }

        return result;
    }
}
