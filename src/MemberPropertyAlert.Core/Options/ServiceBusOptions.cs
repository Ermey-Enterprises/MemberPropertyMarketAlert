namespace MemberPropertyAlert.Core.Options;

public sealed class ServiceBusOptions
{
    public const string SectionName = "ServiceBus";

    public string? ConnectionString { get; set; }
    public string? ConnectionStringSecretName { get; set; }
    public string? FullyQualifiedNamespace { get; set; }
}
