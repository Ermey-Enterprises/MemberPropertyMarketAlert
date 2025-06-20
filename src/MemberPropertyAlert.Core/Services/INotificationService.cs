using MemberPropertyAlert.Core.Models;

namespace MemberPropertyAlert.Core.Services
{
    public interface INotificationService
    {
        Task<NotificationResult> SendWebhookAsync(PropertyAlert alert, Institution institution);
        Task<NotificationResult> SendBulkWebhookAsync(List<PropertyAlert> alerts, Institution institution);
        Task<NotificationResult> RetryFailedWebhookAsync(PropertyAlert alert, Institution institution);
        Task<List<NotificationResult>> ProcessPendingAlertsAsync();
        Task<bool> ValidateWebhookEndpointAsync(string webhookUrl, string? authHeader = null);
    }

    public interface ISignalRService
    {
        Task SendScanUpdateAsync(string institutionId, ScanUpdateMessage message);
        Task SendAlertNotificationAsync(string institutionId, PropertyAlert alert);
        Task SendSystemStatusAsync(SystemStatusMessage message);
        Task JoinInstitutionGroupAsync(string connectionId, string institutionId);
        Task LeaveInstitutionGroupAsync(string connectionId, string institutionId);
    }

    public class NotificationResult
    {
        public bool IsSuccess { get; set; }
        public int StatusCode { get; set; }
        public string? ResponseBody { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime SentAt { get; set; } = DateTime.UtcNow;
        public TimeSpan ResponseTime { get; set; }
        public int AttemptNumber { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class ScanUpdateMessage
    {
        public string ScanId { get; set; } = string.Empty;
        public ScanStatus Status { get; set; }
        public string Message { get; set; } = string.Empty;
        public int AddressesScanned { get; set; }
        public int TotalAddresses { get; set; }
        public int AlertsGenerated { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string? CurrentAddress { get; set; }
        public Dictionary<string, object> Data { get; set; } = new();
    }

    public class SystemStatusMessage
    {
        public string Component { get; set; } = string.Empty;
        public SystemHealthStatus Status { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public Dictionary<string, object> Metrics { get; set; } = new();
    }

    public enum SystemHealthStatus
    {
        Healthy,
        Warning,
        Critical,
        Offline
    }

    public class NotificationConfiguration
    {
        public int MaxRetries { get; set; } = 3;
        public List<int> RetryDelaySeconds { get; set; } = new() { 30, 60, 120 };
        public int TimeoutSeconds { get; set; } = 30;
        public bool EnableBatching { get; set; } = true;
        public int BatchSize { get; set; } = 10;
        public int BatchTimeoutMinutes { get; set; } = 5;
        public string DefaultUserAgent { get; set; } = "MemberPropertyAlert/1.0";
        public Dictionary<string, string> DefaultHeaders { get; set; } = new();
    }

    public class SignalRConfiguration
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string HubName { get; set; } = "PropertyAlertHub";
        public bool EnableDetailedErrors { get; set; } = false;
        public int MaxMessageSize { get; set; } = 32768; // 32KB
        public TimeSpan ClientTimeoutInterval { get; set; } = TimeSpan.FromSeconds(30);
        public TimeSpan KeepAliveInterval { get; set; } = TimeSpan.FromSeconds(15);
    }
}
