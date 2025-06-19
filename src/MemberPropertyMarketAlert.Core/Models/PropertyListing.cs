using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace MemberPropertyMarketAlert.Core.Models;

public class PropertyListing
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("mlsNumber")]
    public string? MlsNumber { get; set; }

    [JsonProperty("address")]
    [Required]
    public string Address { get; set; } = string.Empty;

    [JsonProperty("city")]
    [Required]
    public string City { get; set; } = string.Empty;

    [JsonProperty("state")]
    [Required]
    public string State { get; set; } = string.Empty;

    [JsonProperty("zipCode")]
    [Required]
    public string ZipCode { get; set; } = string.Empty;

    [JsonProperty("normalizedAddress")]
    public string NormalizedAddress { get; set; } = string.Empty;

    [JsonProperty("latitude")]
    public double? Latitude { get; set; }

    [JsonProperty("longitude")]
    public double? Longitude { get; set; }

    [JsonProperty("price")]
    public decimal Price { get; set; }

    [JsonProperty("listingDate")]
    public DateTime ListingDate { get; set; }

    [JsonProperty("status")]
    public PropertyStatus Status { get; set; } = PropertyStatus.Active;

    [JsonProperty("propertyType")]
    public PropertyType PropertyType { get; set; } = PropertyType.SingleFamily;

    [JsonProperty("bedrooms")]
    public int? Bedrooms { get; set; }

    [JsonProperty("bathrooms")]
    public decimal? Bathrooms { get; set; }

    [JsonProperty("squareFeet")]
    public int? SquareFeet { get; set; }

    [JsonProperty("lotSize")]
    public decimal? LotSize { get; set; }

    [JsonProperty("yearBuilt")]
    public int? YearBuilt { get; set; }

    [JsonProperty("description")]
    public string? Description { get; set; }

    [JsonProperty("listingAgent")]
    public string? ListingAgent { get; set; }

    [JsonProperty("listingOffice")]
    public string? ListingOffice { get; set; }

    [JsonProperty("dataSource")]
    [Required]
    public string DataSource { get; set; } = string.Empty;

    [JsonProperty("sourceUrl")]
    public string? SourceUrl { get; set; }

    [JsonProperty("imageUrls")]
    public List<string> ImageUrls { get; set; } = new();

    [JsonProperty("createdDate")]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [JsonProperty("updatedDate")]
    public DateTime UpdatedDate { get; set; } = DateTime.UtcNow;

    [JsonProperty("partitionKey")]
    public string PartitionKey => $"{State}_{City}";

    public string GetFullAddress()
    {
        return $"{Address}, {City}, {State} {ZipCode}";
    }

    public void UpdateNormalizedAddress()
    {
        NormalizedAddress = GetFullAddress()
            .ToUpperInvariant()
            .Replace("STREET", "ST")
            .Replace("AVENUE", "AVE")
            .Replace("BOULEVARD", "BLVD")
            .Replace("DRIVE", "DR")
            .Replace("LANE", "LN")
            .Replace("ROAD", "RD")
            .Replace("COURT", "CT")
            .Replace("PLACE", "PL")
            .Replace("CIRCLE", "CIR")
            .Replace("  ", " ")
            .Trim();
    }
}

public enum PropertyStatus
{
    Active,
    Pending,
    Sold,
    Withdrawn,
    Expired,
    Cancelled
}

public enum PropertyType
{
    SingleFamily,
    Townhouse,
    Condominium,
    Duplex,
    Triplex,
    Fourplex,
    Manufactured,
    Land,
    Commercial,
    Other
}
