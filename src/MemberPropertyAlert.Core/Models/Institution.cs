using System.ComponentModel.DataAnnotations;

namespace MemberPropertyAlert.Core.Models
{
    public class Institution
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        [Required]
        public string Name { get; set; } = string.Empty;
        
        [Required]
        [EmailAddress]
        public string ContactEmail { get; set; } = string.Empty;
        
        public string? WebhookUrl { get; set; }
        
        public string? WebhookAuthHeader { get; set; }
        
        public WebhookRetryPolicy RetryPolicy { get; set; } = new();
        
        public bool IsActive { get; set; } = true;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        public List<ScanSchedule> ScanSchedules { get; set; } = new();
        
        // Enhanced notification settings
        public NotificationSettings NotificationSettings { get; set; } = new();
        
        // Institution-specific configuration
        public InstitutionConfiguration Configuration { get; set; } = new();
    }

    public class NotificationSettings
    {
        public List<NotificationDeliveryMethod> DeliveryMethods { get; set; } = new();
        
        public WebhookSettings? WebhookSettings { get; set; }
        
        public EmailSettings? EmailSettings { get; set; }
        
        public CsvSettings? CsvSettings { get; set; }
        
        public bool EnableBatching { get; set; } = true;
        
        public int BatchSize { get; set; } = 10;
        
        public int BatchTimeoutMinutes { get; set; } = 5;
    }

    public class WebhookSettings
    {
        public string Url { get; set; } = string.Empty;
        
        public string? AuthHeader { get; set; }
        
        public Dictionary<string, string> CustomHeaders { get; set; } = new();
        
        public WebhookRetryPolicy RetryPolicy { get; set; } = new();
        
        public int TimeoutSeconds { get; set; } = 30;
        
        public bool VerifySSL { get; set; } = true;
    }

    public class EmailSettings
    {
        public List<string> Recipients { get; set; } = new();
        
        public string? Subject { get; set; }
        
        public EmailFormat Format { get; set; } = EmailFormat.Html;
        
        public bool IncludeAttachments { get; set; } = false;
        
        public string? CustomTemplate { get; set; }
    }

    public class CsvSettings
    {
        public string DeliveryMethod { get; set; } = "email"; // email, webhook, ftp
        
        public string? DeliveryEndpoint { get; set; }
        
        public string Delimiter { get; set; } = ",";
        
        public bool IncludeHeaders { get; set; } = true;
        
        public string DateFormat { get; set; } = "yyyy-MM-dd HH:mm:ss";
        
        public List<string> IncludeFields { get; set; } = new();
    }

    public class InstitutionConfiguration
    {
        public bool UseMockServices { get; set; } = false;
        
        public string? RentCastApiKey { get; set; }
        
        public ScanConfiguration ScanSettings { get; set; } = new();
        
        public AlertConfiguration AlertSettings { get; set; } = new();
        
        public Dictionary<string, object> CustomSettings { get; set; } = new();
    }

    public class ScanConfiguration
    {
        public int MaxConcurrentScans { get; set; } = 5;
        
        public int RateLimitDelayMs { get; set; } = 1000;
        
        public int TimeoutSeconds { get; set; } = 30;
        
        public bool EnableCaching { get; set; } = true;
        
        public int CacheDurationMinutes { get; set; } = 15;
        
        public List<string> ExcludedStates { get; set; } = new();
    }

    public class AlertConfiguration
    {
        public decimal? MinPrice { get; set; }
        
        public decimal? MaxPrice { get; set; }
        
        public int? MinBedrooms { get; set; }
        
        public int? MaxBedrooms { get; set; }
        
        public List<string> PropertyTypes { get; set; } = new();
        
        public int MaxDaysOnMarket { get; set; } = 30;
        
        public bool OnlyNewListings { get; set; } = true;
    }

    public class WebhookRetryPolicy
    {
        public int MaxRetries { get; set; } = 3;
        
        public List<int> BackoffSeconds { get; set; } = new() { 30, 60, 120 };
    }

    public class ScanSchedule
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        [Required]
        public string Name { get; set; } = string.Empty;
        
        [Required]
        public string CronExpression { get; set; } = string.Empty;
        
        public string? AddressFilter { get; set; }
        
        public bool IsActive { get; set; } = true;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public enum NotificationDeliveryMethod
    {
        Webhook,
        Email,
        Csv
    }

    public enum EmailFormat
    {
        Html,
        Text
    }
}
