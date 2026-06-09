namespace FamilyMoney.Sync;

public static class SyncContext
{
    private static readonly AsyncLocal<bool> Applying = new();

    public static bool IsApplying => Applying.Value;

    public static IDisposable EnterApplyScope()
    {
        Applying.Value = true;
        return new ApplyScope();
    }

    private sealed class ApplyScope : IDisposable
    {
        public void Dispose() => Applying.Value = false;
    }
}
