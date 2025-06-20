using System.Text.Json.Serialization;

namespace MemberPropertyAlert.Core.Models;

public class PropertyListing
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("address")]
    public string Address { get; set; } = string.Empty;

    [JsonPropertyName("city")]
    public string City { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("zipCode")]
    public string ZipCode { get; set; } = string.Empty;

    [JsonPropertyName("price")]
    public decimal? Price { get; set; }

    [JsonPropertyName("bedrooms")]
    public int? Bedrooms { get; set; }

    [JsonPropertyName("bathrooms")]
    public decimal? Bathrooms { get; set; }

    [JsonPropertyName("squareFootage")]
    public int? SquareFootage { get; set; }

    [JsonPropertyName("listingDate")]
    public DateTime? ListingDate { get; set; }

    [JsonPropertyName("daysOnMarket")]
    public int? DaysOnMarket { get; set; }

    [JsonPropertyName("propertyType")]
    public string PropertyType { get; set; } = string.Empty;

    [JsonPropertyName("mlsNumber")]
    public string MlsNumber { get; set; } = string.Empty;

    [JsonPropertyName("listingAgent")]
    public string ListingAgent { get; set; } = string.Empty;

    [JsonPropertyName("listingOffice")]
    public string ListingOffice { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("photos")]
    public List<string> Photos { get; set; } = new();

    [JsonPropertyName("latitude")]
    public double? Latitude { get; set; }

    [JsonPropertyName("longitude")]
    public double? Longitude { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("lastUpdated")]
    public DateTime? LastUpdated { get; set; }

    // Additional properties for matching
    public string FormattedAddress => $"{Address}, {City}, {State} {ZipCode}".Trim();
    
    public bool IsActive => Status?.ToLower() == "active" || Status?.ToLower() == "for sale";
}
