namespace MemberPropertyAlert.Core.Options;

public sealed class SignalROptions
{
    public const string SectionName = "SignalR";

    public string HubEndpoint { get; set; } = string.Empty;
    public string HubName { get; set; } = "alerts";
    public string? AccessKey { get; set; }
}
