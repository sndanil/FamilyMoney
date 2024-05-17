using FamilyMoney.ViewModels;

namespace FamilyMoney.Messages;

public class TransactionGroupDeleteMessage
{
    public required TransactionGroupViewModel Element { get; init; }
}
