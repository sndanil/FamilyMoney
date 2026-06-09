using FamilyMoney.Models.Sync;

namespace FamilyMoney.DataAccess;

public sealed class SyncPendingChanges
{
    public IReadOnlyList<SyncOutboxEntry> Entities { get; init; } = [];

    public IReadOnlyList<SyncImageOutboxEntry> Images { get; init; } = [];

    public bool HasChanges => Entities.Count > 0 || Images.Count > 0;
}
