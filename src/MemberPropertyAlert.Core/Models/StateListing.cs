using System.ComponentModel.DataAnnotations;

namespace MemberPropertyAlert.Core.Models
{
    public class StateListing
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        [Required]
        public string State { get; set; } = string.Empty;
        
        [Required]
        public DateTime ScanDate { get; set; }
        
        [Required]
        public string Address { get; set; } = string.Empty;
        
        public string? City { get; set; }
        
        public string? ZipCode { get; set; }
        
        public string? ListingId { get; set; }
        
        public decimal? Price { get; set; }
        
        public DateTime? ListDate { get; set; }
        
        public string? PropertyType { get; set; }
        
        public int? Bedrooms { get; set; }
        
        public int? Bathrooms { get; set; }
        
        public int? SquareFeet { get; set; }
        
        public string? MlsId { get; set; }
        
        public string? Status { get; set; }
        
        public bool IsNewListing { get; set; } = false;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public Dictionary<string, object> RawData { get; set; } = new();
        
        // Computed property for efficient address matching
        public string NormalizedAddress => 
            $"{Address?.Trim().ToUpperInvariant()}, {City?.Trim().ToUpperInvariant()}, {State?.Trim().ToUpperInvariant()} {ZipCode?.Trim()}";
    }
    
    public class StateListingSummary
    {
        public string State { get; set; } = string.Empty;
        public DateTime ScanDate { get; set; }
        public int TotalListings { get; set; }
        public int NewListings { get; set; }
        public int MatchedProperties { get; set; }
        public TimeSpan ProcessingTime { get; set; }
        public bool IsSuccess { get; set; } = true;
        public string? ErrorMessage { get; set; }
    }
}
