using FamilyMoney.Models;

namespace FamilyMoney.Messages;

public sealed class TransactionChangedMessage
{
    public Transaction? Before { get; set; }
    public Transaction? After { get; set; }
}
