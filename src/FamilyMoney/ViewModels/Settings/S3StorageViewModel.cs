using CommunityToolkit.Mvvm.ComponentModel;
using FamilyMoney.Configuration;

namespace FamilyMoney.ViewModels.Settings;

public sealed record S3EndpointPreset(
    string Title,
    string ServiceUrl,
    string Region,
    bool ForcePathStyle);

public partial class S3StorageViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial bool Enabled { get; set; }

    [ObservableProperty]
    public partial string ServiceUrl { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Bucket { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Region { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string AccessKey { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SecretKey { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool ForcePathStyle { get; set; }

    [ObservableProperty]
    public partial S3EndpointPreset? SelectedPreset { get; set; }

    public IReadOnlyList<S3EndpointPreset> Presets { get; } =
    [
        new("AWS S3", "https://s3.amazonaws.com", "us-east-1", false),
        new("Яндекс Object Storage", "https://storage.yandexcloud.net", "ru-central1", false),
        new("VK Cloud", "https://hb.vkcs.cloud", "ru-central1", false),
        new("Selectel", "https://s3.storage.selcloud.ru", "ru-1", false),
        new("MinIO / другой S3", string.Empty, string.Empty, true),
    ];

    partial void OnSelectedPresetChanged(S3EndpointPreset? value)
    {
        if (value == null)
        {
            return;
        }

        ServiceUrl = value.ServiceUrl;
        Region = value.Region;
        ForcePathStyle = value.ForcePathStyle;
    }

    public void FillFrom(S3StorageConfiguration config)
    {
        Enabled = config.Enabled;
        ServiceUrl = config.ServiceUrl;
        Bucket = config.Bucket;
        Region = config.Region;
        AccessKey = config.AccessKey;
        SecretKey = config.SecretKey;
        ForcePathStyle = config.ForcePathStyle;
        SelectedPreset = null;
    }

    public S3StorageConfiguration ToConfiguration()
    {
        return new S3StorageConfiguration
        {
            Enabled = Enabled,
            ServiceUrl = ServiceUrl.Trim(),
            Bucket = Bucket.Trim(),
            Region = Region.Trim(),
            AccessKey = AccessKey.Trim(),
            SecretKey = SecretKey,
            ForcePathStyle = ForcePathStyle,
        };
    }

    public void ApplyFrom(S3StorageViewModel source)
    {
        Enabled = source.Enabled;
        ServiceUrl = source.ServiceUrl;
        Bucket = source.Bucket;
        Region = source.Region;
        AccessKey = source.AccessKey;
        SecretKey = source.SecretKey;
        ForcePathStyle = source.ForcePathStyle;
        SelectedPreset = null;
    }
}
