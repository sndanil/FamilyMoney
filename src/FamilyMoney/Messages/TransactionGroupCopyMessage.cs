using FamilyMoney.ViewModels;

namespace FamilyMoney.Messages
{
    public class TransactionGroupCopyMessage
    {
        public required TransactionGroupViewModel Element { get; init; }
    }
}
