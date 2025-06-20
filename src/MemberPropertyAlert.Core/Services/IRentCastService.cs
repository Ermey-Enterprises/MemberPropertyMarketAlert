using MemberPropertyAlert.Core.Models;

namespace MemberPropertyAlert.Core.Services
{
    public interface IRentCastService
    {
        Task<PropertyListing?> GetPropertyListingAsync(string address);
        Task<bool> IsPropertyListedAsync(string address);
        Task<PropertyListing[]> GetRecentListingsAsync(string city, string state, int daysBack = 7);
        Task<PropertyListing[]> GetStateListingsAsync(string state);
        Task<PropertyListing[]> GetNewListingsAsync(string state, DateTime sinceDate);
    }

    public class PropertyListingResult
    {
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
        public PropertyStatus Status { get; set; } = PropertyStatus.Unknown;
        public PropertyListingDetails? ListingDetails { get; set; }
        public string OriginalAddress { get; set; } = string.Empty;
        public string? NormalizedAddress { get; set; }
        public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
        public Dictionary<string, object> RawData { get; set; } = new();
    }

    public class RentCastConfiguration
    {
        public string ApiKey { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = "https://api.rentcast.io/v1";
        public int TimeoutSeconds { get; set; } = 30;
        public int MaxRetries { get; set; } = 3;
        public int RateLimitDelayMs { get; set; } = 1000;
        public bool EnableCaching { get; set; } = true;
        public int CacheDurationMinutes { get; set; } = 15;
    }
}
