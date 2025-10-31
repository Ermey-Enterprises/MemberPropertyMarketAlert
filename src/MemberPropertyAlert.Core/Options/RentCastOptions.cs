using System;

namespace MemberPropertyAlert.Core.Options;

public sealed class RentCastOptions
{
    public const string SectionName = "RentCast";

    public string BaseUrl { get; set; } = "https://api.rentcast.io";
    public string MockBaseUrl { get; set; } = "https://web-mrc-dev-eus2-qiur.azurewebsites.net";
    public string ApiKey { get; set; } = string.Empty;
        public string? MockApiKey { get; set; }
    public bool UseMockInNonProduction { get; set; } = true;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}
