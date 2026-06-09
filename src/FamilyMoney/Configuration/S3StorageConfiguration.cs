namespace FamilyMoney.Configuration;

public sealed class S3StorageConfiguration
{
    public bool Enabled { get; set; }

    public string ServiceUrl { get; set; } = string.Empty;

    public string Bucket { get; set; } = string.Empty;

    public string Region { get; set; } = string.Empty;

    public string AccessKey { get; set; } = string.Empty;

    public string SecretKey { get; set; } = string.Empty;

    public bool ForcePathStyle { get; set; }
}
