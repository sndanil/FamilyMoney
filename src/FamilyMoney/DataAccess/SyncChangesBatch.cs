namespace FamilyMoney.DataAccess;

/// <summary>
/// Порция изменений для peer-синхронизации.
/// <see cref="Next"/> — курсор следующей страницы (следующая миллисекунда после
/// последнего LastChange); <see cref="Watermark"/> — максимальный LastChange
/// в порции (его сохраняем как состояние пары); <see cref="HasMore"/> — остались
/// ли записи.
/// </summary>
public sealed record SyncChangesBatch(
    SyncPendingChanges Changes,
    DateTime Next,
    DateTime Watermark,
    bool HasMore);
