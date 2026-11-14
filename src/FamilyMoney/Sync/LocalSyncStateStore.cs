using FamilyMoney.Configuration;
using FamilyMoney.Utils;
using System.Text.Json;

namespace FamilyMoney.Sync;

public sealed class LocalSyncStateStore
{
    public LocalSyncStateStore()
        : this(GlobalConfiguration.HomeFolder)
    {
    }

    public LocalSyncStateStore(string homeFolder)
    {
        HomeFolder = homeFolder;
        DeviceId = GetOrCreateDeviceId(homeFolder);
    }

    public string HomeFolder { get; }

    public string DeviceId { get; }

    public LocalSyncState Load(Guid databaseSyncId, string deviceId)
    {
        var path = SyncPaths.LocalStatePath(databaseSyncId, HomeFolder);
        if (!File.Exists(path))
        {
            return new LocalSyncState
            {
                DatabaseSyncId = databaseSyncId,
                DeviceId = deviceId,
            };
        }

        var json = File.ReadAllText(path);
        var state = JsonSerializer.Deserialize<LocalSyncState>(json, JsonDefaults.Indented)
            ?? new LocalSyncState { DatabaseSyncId = databaseSyncId, DeviceId = deviceId };
        state.DeviceId = deviceId;
        return state;
    }

    public void Save(LocalSyncState state)
    {
        var path = SyncPaths.LocalStatePath(state.DatabaseSyncId, HomeFolder);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(state, JsonDefaults.Indented);
        File.WriteAllText(path, json);
    }

    public static string GetOrCreateDeviceId(string? homeFolder = null)
    {
        var folder = homeFolder ?? GlobalConfiguration.HomeFolder;
        var path = SyncPaths.DeviceIdPath(folder);
        if (File.Exists(path))
        {
            var existing = File.ReadAllText(path).Trim();
            if (!string.IsNullOrWhiteSpace(existing))
            {
                return existing;
            }
        }

        Directory.CreateDirectory(folder);
        var deviceId = Guid.NewGuid().ToString("N");
        File.WriteAllText(path, deviceId);
        return deviceId;
    }
}
