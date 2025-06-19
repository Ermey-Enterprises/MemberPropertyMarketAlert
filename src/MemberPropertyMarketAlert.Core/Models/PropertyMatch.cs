using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace MemberPropertyMarketAlert.Core.Models;

public class PropertyMatch
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("memberAddressId")]
    [Required]
    public string MemberAddressId { get; set; } = string.Empty;

    [JsonProperty("propertyListingId")]
    [Required]
    public string PropertyListingId { get; set; } = string.Empty;

    [JsonProperty("institutionId")]
    [Required]
    public string InstitutionId { get; set; } = string.Empty;

    [JsonProperty("anonymousReferenceId")]
    [Required]
    public string AnonymousReferenceId { get; set; } = string.Empty;

    [JsonProperty("matchConfidence")]
    public MatchConfidence MatchConfidence { get; set; }

    [JsonProperty("matchScore")]
    public double MatchScore { get; set; }

    [JsonProperty("matchMethod")]
    public MatchMethod MatchMethod { get; set; }

    [JsonProperty("memberAddress")]
    public string MemberAddress { get; set; } = string.Empty;

    [JsonProperty("listingAddress")]
    public string ListingAddress { get; set; } = string.Empty;

    [JsonProperty("listingPrice")]
    public decimal ListingPrice { get; set; }

    [JsonProperty("listingDate")]
    public DateTime ListingDate { get; set; }

    [JsonProperty("propertyStatus")]
    public PropertyStatus PropertyStatus { get; set; }

    [JsonProperty("notificationsSent")]
    public List<NotificationRecord> NotificationsSent { get; set; } = new();

    [JsonProperty("isProcessed")]
    public bool IsProcessed { get; set; } = false;

    [JsonProperty("createdDate")]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [JsonProperty("updatedDate")]
    public DateTime UpdatedDate { get; set; } = DateTime.UtcNow;

    [JsonProperty("partitionKey")]
    public string PartitionKey => InstitutionId;

    public void AddNotification(NotificationType type, string recipient, bool success, string? errorMessage = null)
    {
        NotificationsSent.Add(new NotificationRecord
        {
            Type = type,
            Recipient = recipient,
            SentDate = DateTime.UtcNow,
            Success = success,
            ErrorMessage = errorMessage
        });
        UpdatedDate = DateTime.UtcNow;
    }
}

public class NotificationRecord
{
    [JsonProperty("type")]
    public NotificationType Type { get; set; }

    [JsonProperty("recipient")]
    public string Recipient { get; set; } = string.Empty;

    [JsonProperty("sentDate")]
    public DateTime SentDate { get; set; }

    [JsonProperty("success")]
    public bool Success { get; set; }

    [JsonProperty("errorMessage")]
    public string? ErrorMessage { get; set; }
}

public enum MatchConfidence
{
    Low = 1,
    Medium = 2,
    High = 3,
    Exact = 4
}

public enum MatchMethod
{
    ExactAddress,
    NormalizedAddress,
    FuzzyMatch,
    GeographicProximity
}

public enum NotificationType
{
    Email,
    Webhook,
    SMS,
    Dashboard
}
