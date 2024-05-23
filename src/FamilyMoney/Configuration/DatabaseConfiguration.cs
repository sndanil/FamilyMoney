namespace FamilyMoney.Configuration;

public sealed class DatabaseConfiguration
{
    public required string Path { get;  init; }

    public required string BackupsFolder { get; init; }

    public required int MaxBackups { get; init;}
}
