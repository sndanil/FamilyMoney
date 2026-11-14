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

    public bool IsValid => !Enabled || ToConfiguration().IsConfigured;

    public SyncStorageViewModel()
    {
        S3.PropertyChanged += (_, _) => NotifyConfigurationChanged();
        WebDav.PropertyChanged += (_, _) => NotifyConfigurationChanged();
        Folder.PropertyChanged += (_, _) => NotifyConfigurationChanged();
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
    }

    private void NotifyConfigurationChanged()
    {
        OnPropertyChanged(nameof(IsValid));
        OnPropertyChanged(nameof(EndpointText));
    }
}
