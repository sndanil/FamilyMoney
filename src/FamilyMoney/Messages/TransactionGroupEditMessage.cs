using FamilyMoney.ViewModels;

namespace FamilyMoney.Messages;

public class TransactionGroupEditMessage
{
    public required TransactionGroupViewModel Element { get; init; }
}
