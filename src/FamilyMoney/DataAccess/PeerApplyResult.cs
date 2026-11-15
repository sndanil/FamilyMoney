using FamilyMoney.Models.Sync;

namespace FamilyMoney.DataAccess;

/// <summary>
/// Результат применения peer-дельты. Содержит только записи, которые реально
/// изменили локальную базу; их можно безопасно поставить в облачный outbox.
/// </summary>
public sealed record PeerApplyResult(IReadOnlyList<SyncEntityRecord> ChangedEntities)
{
    public bool HasChanges => ChangedEntities.Count > 0;
}
