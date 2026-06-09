using FamilyMoney.Configuration;

namespace FamilyMoney.Sync;

public static class SyncPaths
{
    public const int SnapshotInterval = 50;

    public static string RootPrefix(DatabaseConfiguration database) =>
        $"familymoney/{database.SyncId:N}";

    public static string ManifestKey(DatabaseConfiguration database) =>
        $"{RootPrefix(database)}/manifest.json";

    public static string DeltaKey(DatabaseConfiguration database, long revision) =>
        $"{RootPrefix(database)}/deltas/{revision:D6}.json";

    public static string SnapshotKey(DatabaseConfiguration database, long revision) =>
        $"{RootPrefix(database)}/snapshots/rev-{revision:D6}.db";

    public static string ImageKey(DatabaseConfiguration database, Guid entityId) =>
        $"{RootPrefix(database)}/images/{entityId:N}";

    public static string LocalStatePath(Guid syncId) =>
        Path.Combine(GlobalConfiguration.HomeFolder, "sync", $"{syncId:N}.json");

    public static string DeviceIdPath() =>
        Path.Combine(GlobalConfiguration.HomeFolder, "device.id");
}
