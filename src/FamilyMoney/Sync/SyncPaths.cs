using FamilyMoney.Configuration;

namespace FamilyMoney.Sync;

public static class SyncPaths
{
    public const int SnapshotInterval = 50;

    public static string RootPrefix(DatabaseConfiguration database) =>
        $"familymoney/{database.SyncId:N}";

    public static string ManifestKey(DatabaseConfiguration database) =>
        $"{RootPrefix(database)}/manifest.json";

    public static string LockKey(DatabaseConfiguration database) =>
        $"{RootPrefix(database)}/sync.lock";

    public static string DeltaKey(DatabaseConfiguration database, long revision) =>
        $"{RootPrefix(database)}/deltas/{revision:D6}.json";

    public static string SnapshotKey(DatabaseConfiguration database, long revision) =>
        $"{RootPrefix(database)}/snapshots/rev-{revision:D6}.db";

    public static string ImageKey(DatabaseConfiguration database, Guid entityId) =>
        $"{RootPrefix(database)}/images/{entityId:N}";

    public static string LocalStatePath(Guid syncId) =>
        LocalStatePath(syncId, GlobalConfiguration.HomeFolder);

    public static string LocalStatePath(Guid syncId, string homeFolder) =>
        Path.Combine(homeFolder, "sync", $"{syncId:N}.json");

    public static string DeviceIdPath() =>
        DeviceIdPath(GlobalConfiguration.HomeFolder);

    public static string DeviceIdPath(string homeFolder) =>
        Path.Combine(homeFolder, "device.id");
}
