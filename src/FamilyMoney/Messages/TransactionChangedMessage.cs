using FamilyMoney.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FamilyMoney.Messages
{
    public sealed class TransactionChangedMessage
    {
        public Transaction? Before { get; set; }
        public Transaction? After { get; set; }
    }
}
