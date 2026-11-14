namespace FamilyMoney.Sync;

/// <summary>
/// Хранилище не поддерживает условную запись. Синхронизация продолжается
/// без неё, но с проверкой результата после записи.
/// </summary>
public sealed class SyncWriteNotSupportedException : Exception
{
    public SyncWriteNotSupportedException(string message)
        : base(message)
    {
    }
}
