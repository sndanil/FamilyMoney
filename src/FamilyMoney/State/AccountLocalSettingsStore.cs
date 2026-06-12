using FamilyMoney.Configuration;
using FamilyMoney.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FamilyMoney.State;

public record AccountLocalFlags(Guid AccountId, bool IsHidden, bool IsExpanded);

/// <summary>
/// Локальные (не синхронизируемые) настройки счетов: видимость и свёрнутость групп.
/// Хранятся в отдельном файле для каждой базы данных вне файла базы,
/// потому что файл базы целиком подменяется при восстановлении снимка из S3.
/// </summary>
public interface IAccountLocalSettingsStore
{
    bool IsHidden(Guid accountId);

    bool IsExpanded(Guid accountId);

    void SetHidden(Guid accountId, bool value);

    void SetExpanded(Guid accountId, bool value);

    /// <summary>Признак, что начальная миграция флагов из базы уже выполнена.</summary>
    bool IsSeeded { get; }

    /// <summary>Одноразовый перенос флагов из базы данных (старое место хранения).</summary>
    void Seed(IEnumerable<AccountLocalFlags> flags);
}

public sealed class AccountLocalSettingsStore : IAccountLocalSettingsStore
{
    private sealed class SettingsDocument
    {
        public bool Seeded { get; set; }

        public Dictionary<Guid, AccountFlagsDocument> Accounts { get; set; } = [];
    }

    private sealed class AccountFlagsDocument
    {
        public bool IsHidden { get; set; }

        public bool IsExpanded { get; set; }
    }

    private readonly IGlobalConfiguration _configuration;
    private readonly object _lock = new();

    private SettingsDocument? _document;
    private string? _documentPath;

    public AccountLocalSettingsStore(IGlobalConfiguration configuration)
    {
        _configuration = configuration;
    }

    public bool IsHidden(Guid accountId)
    {
        lock (_lock)
        {
            return GetDocument().Accounts.TryGetValue(accountId, out var flags) && flags.IsHidden;
        }
    }

    public bool IsExpanded(Guid accountId)
    {
        lock (_lock)
        {
            return GetDocument().Accounts.TryGetValue(accountId, out var flags) && flags.IsExpanded;
        }
    }

    public void SetHidden(Guid accountId, bool value)
    {
        lock (_lock)
        {
            var document = GetDocument();
            var flags = GetOrAddFlags(document, accountId);
            if (flags.IsHidden == value)
            {
                return;
            }

            flags.IsHidden = value;
            Save(document);
        }
    }

    public void SetExpanded(Guid accountId, bool value)
    {
        lock (_lock)
        {
            var document = GetDocument();
            var flags = GetOrAddFlags(document, accountId);
            if (flags.IsExpanded == value)
            {
                return;
            }

            flags.IsExpanded = value;
            Save(document);
        }
    }

    public bool IsSeeded
    {
        get
        {
            lock (_lock)
            {
                return GetDocument().Seeded;
            }
        }
    }

    public void Seed(IEnumerable<AccountLocalFlags> flags)
    {
        lock (_lock)
        {
            var document = GetDocument();
            if (document.Seeded)
            {
                return;
            }

            foreach (var flag in flags)
            {
                document.Accounts[flag.AccountId] = new AccountFlagsDocument
                {
                    IsHidden = flag.IsHidden,
                    IsExpanded = flag.IsExpanded,
                };
            }

            document.Seeded = true;
            Save(document);
        }
    }

    private static AccountFlagsDocument GetOrAddFlags(SettingsDocument document, Guid accountId)
    {
        if (!document.Accounts.TryGetValue(accountId, out var flags))
        {
            flags = new AccountFlagsDocument();
            document.Accounts[accountId] = flags;
        }

        return flags;
    }

    private SettingsDocument GetDocument()
    {
        var path = GetSettingsPath();
        if (_document != null && string.Equals(_documentPath, path, StringComparison.OrdinalIgnoreCase))
        {
            return _document;
        }

        _document = Load(path);
        _documentPath = path;
        return _document;
    }

    private static SettingsDocument Load(string path)
    {
        if (!File.Exists(path))
        {
            return new SettingsDocument();
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<SettingsDocument>(json, JsonDefaults.Indented) ?? new SettingsDocument();
        }
        catch (JsonException)
        {
            return new SettingsDocument();
        }
    }

    private void Save(SettingsDocument document)
    {
        var path = _documentPath ?? GetSettingsPath();
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(document, JsonDefaults.Indented);
        File.WriteAllText(path, json);
    }

    private string GetSettingsPath()
    {
        var database = _configuration.GetSelectedDatabase();
        var key = database.SyncId != Guid.Empty
            ? database.SyncId.ToString("N")
            : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(database.GetResolvedPath()))).ToLowerInvariant();

        return Path.Combine(GlobalConfiguration.HomeFolder, "accounts", $"{key}.json");
    }
}
