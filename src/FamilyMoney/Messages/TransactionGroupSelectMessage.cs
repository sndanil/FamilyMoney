using FamilyMoney.ViewModels;

namespace FamilyMoney.Messages;

public class TransactionGroupSelectMessage
{
    public required BaseTransactionsGroupViewModel Element { get; init; }
}
