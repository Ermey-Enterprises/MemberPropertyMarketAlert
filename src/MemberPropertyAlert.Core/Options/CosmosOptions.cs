namespace MemberPropertyAlert.Core.Options;

public sealed class CosmosOptions
{
    public const string SectionName = "Cosmos";

    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = "MemberPropertyMarketAlert";
    public string InstitutionsContainerName { get; set; } = "institutions";
    public string AddressesContainerName { get; set; } = "addresses";
    public string ScansContainerName { get; set; } = "scans";
    public string AlertsContainerName { get; set; } = "alerts";
}
