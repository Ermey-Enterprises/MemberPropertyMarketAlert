using System.ComponentModel.DataAnnotations;

namespace MemberPropertyAlert.Core.Models
{
    public class MemberAddress
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        [Required]
        public string InstitutionId { get; set; } = string.Empty;
        
        [Required]
        public string AnonymousMemberId { get; set; } = string.Empty;
        
        [Required]
        public string StreetAddress { get; set; } = string.Empty;
        
        [Required]
        public string City { get; set; } = string.Empty;
        
        [Required]
        public string State { get; set; } = string.Empty;
        
        [Required]
        public string ZipCode { get; set; } = string.Empty;
        
        public string FullAddress => $"{StreetAddress}, {City}, {State} {ZipCode}";
        
        public PropertyStatus LastKnownStatus { get; set; } = PropertyStatus.NotListed;
        
        public DateTime? LastCheckedAt { get; set; }
        
        public DateTime? LastStatusChangeAt { get; set; }
        
        public string? Priority { get; set; } = "standard";
        
        public bool IsActive { get; set; } = true;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public enum PropertyStatus
    {
        NotListed,
        Listed,
        UnderContract,
        Sold,
        OffMarket,
        Unknown
    }
}
