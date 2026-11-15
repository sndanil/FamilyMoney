using CommunityToolkit.Mvvm.ComponentModel;
using FamilyMoney.Configuration;

namespace FamilyMoney.ViewModels.Settings;

public sealed record SyncProviderOption(SyncProvider Value, string Title);

public partial class SyncStorageViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial bool Enabled { get; set; }

    [ObservableProperty]
    public partial SyncProviderOption? SelectedProvider { get; set; } =
        new(SyncProvider.None, "Не синхронизировать");

    public IReadOnlyList<SyncProviderOption> Providers { get; } =
    [
        new(SyncProvider.None, "Не синхронизировать"),
        new(SyncProvider.S3, "S3-совместимое хранилище"),
        new(SyncProvider.WebDav, "WebDAV"),
        new(SyncProvider.Folder, "Папка на диске"),
    ];

    public S3StorageViewModel S3 { get; } = new();

    public WebDavStorageViewModel WebDav { get; } = new();

    public FolderStorageViewModel Folder { get; } = new();

    public SyncEncryptionViewModel Encryption { get; } = new();

    [ObservableProperty]
    public partial bool PublishPeerChangesToCloud { get; set; }

    public SyncProvider Provider => SelectedProvider?.Value ?? SyncProvider.None;

    public bool IsS3 => Provider == SyncProvider.S3;

    public bool IsWebDav => Provider == SyncProvider.WebDav;

    public bool IsFolder => Provider == SyncProvider.Folder;

    public bool IsNone => Provider == SyncProvider.None;

    public string ProviderName => Provider switch
    {
        SyncProvider.S3 => "S3",
        SyncProvider.WebDav => "WebDAV",
        SyncProvider.Folder => "Папка",
        _ => "не настроена",
    };

    public string EndpointText => Provider switch
    {
        SyncProvider.S3 => S3.Bucket,
        SyncProvider.WebDav => WebDav.Url,
        SyncProvider.Folder => Folder.Path,
        _ => string.Empty,
    };

    public bool IsValid
    {
        get
        {
            if (!Enabled)
            {
                return true;
            }

            var providerValid = Provider switch
            {
                SyncProvider.S3 => !string.IsNullOrWhiteSpace(S3.Bucket)
                    && !string.IsNullOrWhiteSpace(S3.AccessKey)
                    && !string.IsNullOrWhiteSpace(S3.SecretKey),
                SyncProvider.WebDav => Uri.TryCreate(WebDav.Url.Trim(), UriKind.Absolute, out var uri)
                    && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp),
                SyncProvider.Folder => !string.IsNullOrWhiteSpace(Folder.Path),
                _ => false,
            };

            var encryptionReady = Encryption.Enabled
                ? Encryption.HasKey || Encryption.HasPassword || Encryption.HasRecoveryCode
                : !Encryption.HasKey;

            return providerValid && encryptionReady;
        }
    }

    public SyncStorageViewModel()
    {
        S3.PropertyChanged += (_, _) => NotifyConfigurationChanged();
        WebDav.PropertyChanged += (_, _) => NotifyConfigurationChanged();
        Folder.PropertyChanged += (_, _) => NotifyConfigurationChanged();
        Encryption.PropertyChanged += (_, _) => NotifyConfigurationChanged();
    }

    public bool EnsureEncryptionReady()
    {
        var ready = Encryption.EnsureReady();
        NotifyConfigurationChanged();
        return ready;
    }

    partial void OnEnabledChanged(bool value)
    {
        if (value && Provider == SyncProvider.None)
        {
            SelectedProvider = Providers.First(item => item.Value == SyncProvider.S3);
        }

        NotifyConfigurationChanged();
    }

    partial void OnSelectedProviderChanged(SyncProviderOption? value)
    {
        OnPropertyChanged(nameof(Provider));
        OnPropertyChanged(nameof(IsS3));
        OnPropertyChanged(nameof(IsWebDav));
        OnPropertyChanged(nameof(IsFolder));
        OnPropertyChanged(nameof(IsNone));
        OnPropertyChanged(nameof(ProviderName));
        NotifyConfigurationChanged();
    }

    public void FillFrom(SyncStorageConfiguration config)
    {
        SelectedProvider = Providers.FirstOrDefault(item => item.Value == config.Provider)
            ?? Providers[0];
        Enabled = config.Enabled;
        S3.FillFrom(config.S3);
        WebDav.FillFrom(config.WebDav);
        Folder.FillFrom(config.Folder);
        Encryption.FillFrom(config.Encryption);
        PublishPeerChangesToCloud = config.PublishPeerChangesToCloud;
    }

    public SyncStorageConfiguration ToConfiguration()
    {
        var s3 = S3.ToConfiguration();
        s3.Enabled = Enabled && Provider == SyncProvider.S3;

        return new SyncStorageConfiguration
        {
            Provider = Provider,
            Enabled = Enabled,
            S3 = s3,
            WebDav = WebDav.ToConfiguration(),
            Folder = Folder.ToConfiguration(),
            Encryption = Encryption.ToConfiguration(),
            PublishPeerChangesToCloud = PublishPeerChangesToCloud,
        };
    }

    public void ApplyFrom(SyncStorageViewModel source)
    {
        SelectedProvider = Providers.FirstOrDefault(item => item.Value == source.Provider)
            ?? Providers[0];
        Enabled = source.Enabled;
        S3.ApplyFrom(source.S3);
        WebDav.ApplyFrom(source.WebDav);
        Folder.ApplyFrom(source.Folder);
        Encryption.ApplyFrom(source.Encryption);
        PublishPeerChangesToCloud = source.PublishPeerChangesToCloud;
    }

    private void NotifyConfigurationChanged()
    {
        OnPropertyChanged(nameof(IsValid));
        OnPropertyChanged(nameof(EndpointText));
    }
}
