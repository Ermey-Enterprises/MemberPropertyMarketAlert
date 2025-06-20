using System.ComponentModel.DataAnnotations;

namespace MemberPropertyAlert.Core.Models
{
    public class PropertyAlert
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        [Required]
        public string InstitutionId { get; set; } = string.Empty;
        
        [Required]
        public string AddressId { get; set; } = string.Empty;
        
        [Required]
        public string AnonymousMemberId { get; set; } = string.Empty;
        
        public string FullAddress { get; set; } = string.Empty;
        
        public PropertyStatus PreviousStatus { get; set; }
        
        public PropertyStatus NewStatus { get; set; }
        
        public PropertyListingDetails? ListingDetails { get; set; }
        
        public AlertStatus Status { get; set; } = AlertStatus.Pending;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? ProcessedAt { get; set; }
        
        public DateTime? WebhookSentAt { get; set; }
        
        public int WebhookAttempts { get; set; } = 0;
        
        public string? WebhookResponse { get; set; }
        
        public string? ErrorMessage { get; set; }
        
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class PropertyListingDetails
    {
        public string? ListPrice { get; set; }
        
        public string? MlsId { get; set; }
        
        public DateTime? ListedDate { get; set; }
        
        public int? DaysOnMarket { get; set; }
        
        public string? ListingAgent { get; set; }
        
        public string? ListingAgentPhone { get; set; }
        
        public string? ListingUrl { get; set; }
        
        public string? PropertyType { get; set; }
        
        public int? Bedrooms { get; set; }
        
        public int? Bathrooms { get; set; }
        
        public int? SquareFeet { get; set; }
        
        public string? Description { get; set; }
        
        public List<string> Photos { get; set; } = new();
        
        public Dictionary<string, object> AdditionalData { get; set; } = new();
    }

    public enum AlertStatus
    {
        Pending,
        Processing,
        Sent,
        Failed,
        Cancelled
    }
}
