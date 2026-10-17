namespace Trail.Infrastructure.Identity;

public sealed class DataProtectionPersistenceOptions
{
    public const string SectionName = "DataProtection";

    public string KeysPath { get; set; } = "/data/keys";
}
