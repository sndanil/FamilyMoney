namespace FamilyMoney.DataAccess;

/// <summary>
/// Результат применения изображений из дельты. Если есть неудачные,
/// ревизия не должна считаться полностью применённой.
/// </summary>
public sealed record SyncImagesApplyResult(IReadOnlyList<Guid> Failed)
{
    public static SyncImagesApplyResult Empty { get; } = new([]);

    public bool HasFailures => Failed.Count > 0;
}
