namespace FamilyMoney.Sync;

public interface ISyncService
{
    bool IsEnabled { get; }

    Task<SyncResult> SyncAsync(CancellationToken cancellationToken = default);
}
