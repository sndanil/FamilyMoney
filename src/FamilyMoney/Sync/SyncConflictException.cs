namespace FamilyMoney.Sync;

/// <summary>
/// Объект в хранилище изменился между чтением и записью: условная запись
/// не прошла. Синхронизацию нужно повторить, перечитав манифест.
/// </summary>
public sealed class SyncConflictException : Exception
{
    public SyncConflictException(string message)
        : base(message)
    {
    }
}
