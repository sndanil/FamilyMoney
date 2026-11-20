using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Avalonia.Platform;

namespace FamilyMoney.Navigation.Services;

public sealed record PackAssetView(
    string PackId,
    string AssetId,
    string Name,
    string Kind,
    string Background,
    string[] Aliases,
    string Spec,
    Uri Uri);

/// <summary>
/// Читает поставленные с приложением наборы картинок. Сейчас известен один
/// набор — banks; новые добавляются в KnownPacks и в Assets/Packs.
/// </summary>
public sealed class PackCatalog
{
    private static readonly string[] KnownPacks = ["banks"];

    private static readonly Lazy<PackCatalog> LazyInstance = new(() => new PackCatalog());

    private readonly Dictionary<string, PackAssetView> _byKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<PackAssetView> _assets = [];

    public static PackCatalog Instance => LazyInstance.Value;

    public IReadOnlyList<PackAssetView> Assets => _assets;

    private PackCatalog()
    {
        foreach (var packId in KnownPacks)
        {
            LoadPack(packId);
        }
    }

    public PackAssetView? Resolve(string packId, string assetId)
    {
        return _byKey.GetValueOrDefault($"{packId}/{assetId}");
    }

    private void LoadPack(string packId)
    {
        try
        {
            var manifestUri = new Uri($"avares://FamilyMoney.Navigation/Assets/Packs/{packId}/pack.json");
            using var stream = AssetLoader.Open(manifestUri);
            var manifest = JsonSerializer.Deserialize<PackManifest>(
                stream,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (manifest is null || manifest.Assets.Count == 0)
            {
                return;
            }

            foreach (var asset in manifest.Assets)
            {
                if (string.IsNullOrWhiteSpace(asset.Id) || string.IsNullOrWhiteSpace(asset.File))
                {
                    continue;
                }

                var uri = new Uri($"avares://FamilyMoney.Navigation/Assets/Packs/{packId}/{asset.File}");
                var view = new PackAssetView(
                    packId,
                    asset.Id,
                    asset.Name,
                    asset.Kind,
                    string.IsNullOrWhiteSpace(asset.Background) ? "#FFFFFF" : asset.Background,
                    asset.Aliases ?? [],
                    $"pack:{packId}/{asset.Id}",
                    uri);

                _assets.Add(view);
                _byKey[$"{packId}/{asset.Id}"] = view;
            }
        }
        catch (Exception)
        {
            // Битый/отсутствующий манифест не должен ломать приложение.
        }
    }

    private sealed class PackManifest
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public int Version { get; set; }
        public List<PackAsset> Assets { get; set; } = [];
    }

    private sealed class PackAsset
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string File { get; set; } = "";
        public string Background { get; set; } = "#FFFFFF";
        public string Kind { get; set; } = "logo";
        public string[] Aliases { get; set; } = [];
    }
}
