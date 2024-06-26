﻿using System;

namespace FamilyMoney.Models;

public abstract class SubCategory
{
    public required Guid Id { get; set; }
    public Guid? CategoryId { get; set; }
    public required string Name { get; set; }

    protected SubCategory()
    {
        Id = Guid.NewGuid();
        Name = string.Empty;
    }
}

public sealed class DebetSubCategory: SubCategory
{

}

public sealed class CreditSubCategory : SubCategory
{

}

public sealed class TransferSubCategory : SubCategory
{

}
