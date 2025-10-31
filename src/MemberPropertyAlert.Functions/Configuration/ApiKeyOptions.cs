namespace MemberPropertyAlert.Functions.Configuration;

public sealed class ApiKeyOptions
{
    public const string SectionName = "ApiKey";

    public string? HeaderName { get; set; } = "x-api-key";
    public string? AdminKey { get; set; }
    public string? HashAlgorithm { get; set; } = "SHA256";
}
