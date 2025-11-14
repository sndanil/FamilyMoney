using FamilyMoney.Models;
using FamilyMoney.State;
using System;

namespace FamilyMoney.Messages;

public record AccountSelectMessage(Guid? AccountId);

public record AccountExpandMessage(Guid? AccountId, bool IsExpanded);

public record MainStateChangedMessage(MainState State);

public record CategoryUpdateMessage(Guid? CategoryId);

public record TransactionChangedMessage(Transaction? Before, Transaction? After);
