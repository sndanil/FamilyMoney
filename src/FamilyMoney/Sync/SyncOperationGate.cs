namespace FamilyMoney.Sync;

/// <summary>
/// Общий gate для облачной и Wi-Fi-синхронизации: они не должны одновременно
/// применять дельты и пересчитывать базы.
/// </summary>
public sealed class SyncOperationGate
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<IDisposable?> TryEnterAsync(
        CancellationToken cancellationToken = default)
    {
        if (!await _gate.WaitAsync(0, cancellationToken))
        {
            return null;
        }

        return new Lease(_gate);
    }

    private sealed class Lease : IDisposable
    {
        private SemaphoreSlim? _gate;

        public Lease(SemaphoreSlim gate)
        {
            _gate = gate;
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _gate, null)?.Release();
        }
    }
}
