namespace FamilyMoney.Sync;

/// <summary>
/// Условие записи объекта в хранилище. Позволяет не перезаписать чужую
/// версию манифеста или создать объект только один раз (например, блокировку).
/// </summary>
public readonly record struct SyncWriteCondition(string? IfMatchEtag, bool IfNoneMatch)
{
    public static SyncWriteCondition None => default;

    public static SyncWriteCondition CreateOnly => new(null, true);

    public static SyncWriteCondition IfMatch(string etag) => new(etag, false);

    public bool IsConditional => IfNoneMatch || !string.IsNullOrWhiteSpace(IfMatchEtag);
}
