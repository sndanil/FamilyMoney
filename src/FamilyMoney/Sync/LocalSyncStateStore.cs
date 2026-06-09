using FamilyMoney.Configuration;
using System.Text.Json;

namespace FamilyMoney.Sync;

public sealed class LocalSyncStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public LocalSyncState Load(Guid databaseSyncId, string deviceId)
    {
        var path = SyncPaths.LocalStatePath(databaseSyncId);
        if (!File.Exists(path))
        {
            return new LocalSyncState
            {
                DatabaseSyncId = databaseSyncId,
                DeviceId = deviceId,
            };
        }

        var json = File.ReadAllText(path);
        var state = JsonSerializer.Deserialize<LocalSyncState>(json, JsonOptions)
            ?? new LocalSyncState { DatabaseSyncId = databaseSyncId, DeviceId = deviceId };
        state.DeviceId = deviceId;
        return state;
    }

    public void Save(LocalSyncState state)
    {
        var path = SyncPaths.LocalStatePath(state.DatabaseSyncId);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(state, JsonOptions);
        File.WriteAllText(path, json);
    }

    public static string GetOrCreateDeviceId()
    {
        var path = SyncPaths.DeviceIdPath();
        if (File.Exists(path))
        {
            var existing = File.ReadAllText(path).Trim();
            if (!string.IsNullOrWhiteSpace(existing))
            {
                return existing;
            }
        }

        Directory.CreateDirectory(GlobalConfiguration.HomeFolder);
        var deviceId = Guid.NewGuid().ToString("N");
        File.WriteAllText(path, deviceId);
        return deviceId;
    }
}
