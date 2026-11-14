namespace FamilyMoney.Sync;

/// <summary>Ошибка ключа или расшифровки данных синхронизации.</summary>
public sealed class SyncEncryptionException : Exception
{
    public SyncEncryptionException(string message)
        : base(message)
    {
    }
}
