#nullable enable
#r "nuget: SkiaSharp, 3.119.4"

// Проверяет банковский набор: манифест, уникальные id, наличие файлов,
// размер 256×256, kind и NOTICE. Запуск из корня репозитория:
//   dotnet script resources/validate_bank_pack.csx

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using SkiaSharp;

var root = Directory.GetCurrentDirectory();
var packDir = Path.Combine(root, "src", "FamilyMoney.Navigation", "Assets", "Packs", "banks");
var manifestPath = Path.Combine(packDir, "pack.json");
var errors = new List<string>();

if (!File.Exists(manifestPath))
{
    Console.Error.WriteLine($"Нет манифеста: {manifestPath}");
    Environment.Exit(1);
}

var options = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
};
var manifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(manifestPath), options);
if (manifest == null || manifest.Assets.Count == 0)
{
    Console.Error.WriteLine("Манифест пустой или не разобрался.");
    Environment.Exit(1);
}

var ids = new HashSet<string>(StringComparer.Ordinal);
foreach (var asset in manifest.Assets)
{
    if (string.IsNullOrWhiteSpace(asset.Id) || string.IsNullOrWhiteSpace(asset.File))
    {
        errors.Add("Ассет без id/file.");
        continue;
    }

    if (!ids.Add(asset.Id))
    {
        errors.Add($"Дубликат id: {asset.Id}");
    }

    if (asset.Kind is not ("logo" or "card"))
    {
        errors.Add($"{asset.Id}: неизвестный kind '{asset.Kind}'.");
    }

    var path = Path.Combine(packDir, asset.File);
    if (!File.Exists(path))
    {
        errors.Add($"{asset.Id}: нет файла {asset.File}");
        continue;
    }

    using var codec = SKCodec.Create(path);
    if (codec == null)
    {
        errors.Add($"{asset.Id}: не читается как изображение");
        continue;
    }

    if (codec.Info.Width != 256 || codec.Info.Height != 256)
    {
        errors.Add($"{asset.Id}: {codec.Info.Width}×{codec.Info.Height}, ожидается 256×256");
    }
}

if (!string.IsNullOrWhiteSpace(manifest.Notice) && !File.Exists(Path.Combine(packDir, manifest.Notice)))
{
    errors.Add($"Нет NOTICE: {manifest.Notice}");
}

if (errors.Count > 0)
{
    foreach (var error in errors)
    {
        Console.Error.WriteLine(error);
    }

    Environment.Exit(1);
}

Console.WriteLine($"OK: {manifest.Assets.Count} ассетов, id уникальны, файлы 256×256.");

sealed class Manifest
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public int Version { get; set; }
    public string? Notice { get; set; }
    public List<Asset> Assets { get; set; } = [];
}

sealed class Asset
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string File { get; set; } = "";
    public string Kind { get; set; } = "";
}
