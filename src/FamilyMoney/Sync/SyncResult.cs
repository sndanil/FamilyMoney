namespace FamilyMoney.Sync;

public sealed class SyncResult
{
    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;

    public long AppliedRevision { get; init; }

    public long PublishedRevision { get; init; }

    public static SyncResult Skipped(string message) => new() { Success = true, Message = message };

    public static SyncResult Completed(string message, long applied, long published) =>
        new() { Success = true, Message = message, AppliedRevision = applied, PublishedRevision = published };

    public static SyncResult Failed(string message) => new() { Success = false, Message = message };
}
