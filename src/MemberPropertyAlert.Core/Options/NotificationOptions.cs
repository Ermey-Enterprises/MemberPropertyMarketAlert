namespace MemberPropertyAlert.Core.Options;

public sealed class NotificationOptions
{
    public const string SectionName = "Notifications";

    public bool EnableEmail { get; set; }
    public bool EnableSms { get; set; }
    public bool EnableWebhook { get; set; } = true;
    public string? DefaultWebhookUrl { get; set; }
    public string? DefaultEmailFrom { get; set; }
    public string? DefaultSmsFrom { get; set; }
    public string AlertQueueName { get; set; } = "member-alerts";
}
