namespace FamilyMoney.Configuration;

public sealed class RootConfiguration
{
    public const int DefaultDepositWarningDays = 7;

    public const int MaxDepositWarningDays = 3650;

    public DatabaseConfiguration[] Databases { get; set; } = [];

    public int SelectedDatabaseIndex { get; set; }

    public TransactionsViewConfiguration Transactions { get; set; } = new();

    /// <summary>Ширина списка счетов в главном окне (локальная настройка пользователя).</summary>
    public int? AccountsPaneWidth { get; set; }

    /// <summary>
    /// За сколько дней до окончания подсвечивать вклад. Пусто — значение по умолчанию.
    /// 0 показывает пометку только в день окончания. Локально для устройства.
    /// </summary>
    public int? DepositWarningDays { get; set; }

    public int GetDepositWarningDays()
    {
        if (DepositWarningDays is not int days || days < 0)
        {
            return DefaultDepositWarningDays;
        }

        return Math.Min(days, MaxDepositWarningDays);
    }
}
