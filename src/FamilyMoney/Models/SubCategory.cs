using System;

namespace FamilyMoney.Models;

public abstract class SubCategory : ISyncable
{
    public required Guid Id { get; set; }
    public Guid? CategoryId { get; set; }
    public required string Name { get; set; }

    public DateTime LastChange { get; set; }

    public DateTime? DeletedAt { get; set; }

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
