namespace FamilyMoney.Configuration;

public sealed class RootConfiguration
{
    public DatabaseConfiguration[] Databases { get; set; } = [];

    public int SelectedDatabaseIndex { get; set; }

    public TransactionsViewConfiguration Transactions { get; set; } = new();
}
