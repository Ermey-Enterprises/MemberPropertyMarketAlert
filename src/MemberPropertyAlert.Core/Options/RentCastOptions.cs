namespace MemberPropertyAlert.Core.Options;

public sealed class RentCastOptions
{
    public const string SectionName = "RentCast";

    public string BaseUrl { get; set; } = "https://api.rentcast.io";
    public string ApiKey { get; set; } = string.Empty;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}
