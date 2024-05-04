using FamilyMoney.ViewModels;
using System;

namespace FamilyMoney.Messages
{
    public class TransactionGroupSelectMessage
    {
        public required BaseTransactionsGroupViewModel Element { get; init; }
    }
}
