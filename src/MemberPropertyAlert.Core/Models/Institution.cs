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
}
