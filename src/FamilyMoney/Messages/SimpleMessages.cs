using FamilyMoney.Models;
using FamilyMoney.State;
using FamilyMoney.ViewModels;
using System;

namespace FamilyMoney.Messages;

public record AccountSelectMessage(Guid? AccountId);

public record MainStateChangedMessage(MainState State);

public record CategoryUpdateMessage(Guid? CategoryId);

public record TransactionChangedMessage(Transaction? Before, Transaction? After);

public record TransactionGroupCopyMessage(TransactionGroupViewModel Element);

public record TransactionGroupDeleteMessage(TransactionGroupViewModel Element);

public record TransactionGroupEditMessage(TransactionGroupViewModel Element);

public record TransactionGroupExpandMessage(BaseTransactionsGroupViewModel Element);

public record TransactionGroupSelectMessage(BaseTransactionsGroupViewModel Element);
