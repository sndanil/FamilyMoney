using FamilyMoney.Configuration;
using FamilyMoney.Utils;
using System.Text.Json;

namespace FamilyMoney.Sync.Peer;

public sealed class PeerSyncStateStore
{
    public PeerSyncStateStore()
        : this(GlobalConfiguration.HomeFolder)
    {
    }

    public PeerSyncStateStore(string homeFolder)
    {
        HomeFolder = homeFolder;
    }

    public string HomeFolder { get; }

    public PeerSyncState Load(Guid syncId, string peerDeviceId)
    {
        var path = PathFor(syncId, peerDeviceId);
        if (!File.Exists(path))
        {
            return new PeerSyncState
            {
                DatabaseSyncId = syncId,
                PeerDeviceId = peerDeviceId,
            };
        }

        var json = File.ReadAllText(path);
        var state = JsonSerializer.Deserialize<PeerSyncState>(json, JsonDefaults.Indented)
            ?? new PeerSyncState
            {
                DatabaseSyncId = syncId,
                PeerDeviceId = peerDeviceId,
            };

        state.DatabaseSyncId = syncId;
        state.PeerDeviceId = peerDeviceId;
        return state;
    }

    public void Save(PeerSyncState state)
    {
        var path = PathFor(state.DatabaseSyncId, state.PeerDeviceId);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(state, JsonDefaults.Indented);
        File.WriteAllText(path, json);
    }

    private string PathFor(Guid syncId, string peerDeviceId)
    {
        if (!IsSafePeerId(peerDeviceId))
        {
            throw new ArgumentException("Некорректный идентификатор peer-устройства.", nameof(peerDeviceId));
        }

        return SyncPaths.PeerStatePath(syncId, peerDeviceId, HomeFolder);
    }

    private static bool IsSafePeerId(string peerDeviceId) =>
        !string.IsNullOrWhiteSpace(peerDeviceId)
        && peerDeviceId.All(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_');
}
