namespace FamilyMoney.Models;

public interface ISyncable
{
    Guid Id { get; }

    DateTime LastChange { get; set; }

    DateTime? DeletedAt { get; set; }
}
