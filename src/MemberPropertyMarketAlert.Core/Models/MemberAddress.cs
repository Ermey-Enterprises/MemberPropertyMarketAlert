using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace MemberPropertyMarketAlert.Core.Models;

public class MemberAddress
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("institutionId")]
    [Required]
    public string InstitutionId { get; set; } = string.Empty;

    [JsonProperty("anonymousReferenceId")]
    [Required]
    public string AnonymousReferenceId { get; set; } = string.Empty;

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

    [JsonProperty("createdDate")]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [JsonProperty("updatedDate")]
    public DateTime UpdatedDate { get; set; } = DateTime.UtcNow;

    [JsonProperty("isActive")]
    public bool IsActive { get; set; } = true;

    [JsonProperty("partitionKey")]
    public string PartitionKey => InstitutionId;

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
