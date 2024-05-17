using System;

namespace FamilyMoney.Messages;

public class CategoryUpdateMessage
{
    public Guid? CategoryId { get; init; }
}
