using System;

namespace FamilyMoney.Models;

public class Account
{
    public Guid Id { get; set; }

    public Guid? ParentId { get; set; }

    public required string Name { get; set; }

    public int Order { get; set; }

    public bool IsGroup { get; set; }
}

