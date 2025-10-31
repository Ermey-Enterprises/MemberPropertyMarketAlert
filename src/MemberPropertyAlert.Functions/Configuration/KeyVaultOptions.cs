namespace MemberPropertyAlert.Functions.Configuration;

public sealed class KeyVaultOptions
{
    public const string SectionName = "KeyVault";

    public string? VaultUri { get; set; }
}
