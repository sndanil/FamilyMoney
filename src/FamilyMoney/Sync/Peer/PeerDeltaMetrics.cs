namespace FamilyMoney.Sync.Peer;

public static class PeerDeltaMetrics
{
    public static int EntityCount(SyncDelta delta) =>
        delta.Accounts.Count
        + delta.Categories.Count
        + delta.SubCategories.Count
        + delta.Transactions.Count;

    public static int ImageCount(SyncDelta delta) => delta.Images.Count;

    /// <summary>Следующая миллисекунда после максимального LastChange в дельте.</summary>
    public static DateTime NextWatermark(SyncDelta delta)
    {
        var max = MaxLastChange(delta);
        return max == DateTime.MaxValue ? max : max.AddMilliseconds(1);
    }

    public static DateTime MaxLastChange(SyncDelta delta)
    {
        var max = DateTime.MinValue;

        foreach (var record in delta.Accounts
                     .Concat(delta.Categories)
                     .Concat(delta.SubCategories)
                     .Concat(delta.Transactions))
        {
            if (record.LastChange > max)
            {
                max = record.LastChange;
            }
        }

        foreach (var image in delta.Images)
        {
            if (image.LastChange > max)
            {
                max = image.LastChange;
            }
        }

        return max;
    }
}
