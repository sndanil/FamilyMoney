namespace FamilyMoney.Sync;

/// <summary>
/// Ключ объекта синхронизации всегда задан относительно корня хранилища,
/// без ведущего «/»: именно в таком виде ключи строит <see cref="SyncPaths"/>
/// и принимают <see cref="ISyncObjectStore"/>.
/// Провайдеры отдают листинг по-разному: FluentStorage поверх S3 возвращает
/// полный путь с ведущим «/». Если такой ключ сравнить с префиксом или отдать
/// в скачивание как есть, манифесты устройств молча перестают находиться,
/// и чужие дельты никогда не применяются.
/// </summary>
public static class SyncObjectKeys
{
    public static string Normalize(string key) => key.TrimStart('/');
}
