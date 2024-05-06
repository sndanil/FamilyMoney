using FamilyMoney.ViewModels;

namespace FamilyMoney.Messages
{
    public class TransactionGroupExpandMessage
    {
        public required BaseTransactionsGroupViewModel Element { get; init; }
    }
}
