using CommunityToolkit.Mvvm.ComponentModel;
using FamilyMoney.Configuration;

namespace FamilyMoney.ViewModels.Settings;

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

    public void FillFrom(S3StorageConfiguration config)
    {
        Enabled = config.Enabled;
        ServiceUrl = config.ServiceUrl;
        Bucket = config.Bucket;
        Region = config.Region;
        AccessKey = config.AccessKey;
        SecretKey = config.SecretKey;
        ForcePathStyle = config.ForcePathStyle;
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
    }
}
