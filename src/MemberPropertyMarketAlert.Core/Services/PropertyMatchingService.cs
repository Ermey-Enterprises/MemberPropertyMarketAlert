using FuzzySharp;
using Microsoft.Extensions.Logging;
using MemberPropertyMarketAlert.Core.Models;

namespace MemberPropertyMarketAlert.Core.Services;

public class PropertyMatchingService : IPropertyMatchingService
{
    private readonly ICosmosDbService _cosmosDbService;
    private readonly ILogger<PropertyMatchingService> _logger;

    // Matching thresholds
    private const double ExactMatchThreshold = 100.0;
    private const double HighConfidenceThreshold = 95.0;
    private const double MediumConfidenceThreshold = 85.0;
    private const double LowConfidenceThreshold = 75.0;
    private const double GeographicProximityThreshold = 0.1; // miles

    public PropertyMatchingService(ICosmosDbService cosmosDbService, ILogger<PropertyMatchingService> logger)
    {
        _cosmosDbService = cosmosDbService;
        _logger = logger;
    }

    public async Task<IEnumerable<PropertyMatch>> FindMatchesAsync(IEnumerable<PropertyListing> propertyListings)
    {
        var matches = new List<PropertyMatch>();

        try
        {
            foreach (var listing in propertyListings)
            {
                var listingMatches = await FindMatchesForListingAsync(listing);
                matches.AddRange(listingMatches);
            }

            _logger.LogInformation("Found {MatchCount} total matches for {ListingCount} listings", 
                matches.Count, propertyListings.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding matches for property listings");
            throw;
        }

        return matches;
    }

    public async Task<IEnumerable<PropertyMatch>> FindMatchesForListingAsync(PropertyListing propertyListing)
    {
        var matches = new List<PropertyMatch>();

        try
        {
            // Get all active member addresses in the same state/city for efficiency
            var memberAddresses = await GetMemberAddressesByLocation(propertyListing.State, propertyListing.City);

            foreach (var memberAddress in memberAddresses)
            {
                var matchResult = CalculateMatch(memberAddress, propertyListing);
                
                if (matchResult.IsMatch)
                {
                    var propertyMatch = CreatePropertyMatch(memberAddress, propertyListing, matchResult);
                    matches.Add(propertyMatch);
                }
            }

            _logger.LogInformation("Found {MatchCount} matches for listing {ListingId}", 
                matches.Count, propertyListing.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding matches for listing {ListingId}", propertyListing.Id);
            throw;
        }

        return matches;
    }

    public async Task<IEnumerable<PropertyMatch>> FindMatchesForInstitutionAsync(string institutionId, IEnumerable<PropertyListing> propertyListings)
    {
        var matches = new List<PropertyMatch>();

        try
        {
            var memberAddresses = await _cosmosDbService.GetMemberAddressesByInstitutionAsync(institutionId);

            foreach (var listing in propertyListings)
            {
                foreach (var memberAddress in memberAddresses)
                {
                    var matchResult = CalculateMatch(memberAddress, listing);
                    
                    if (matchResult.IsMatch)
                    {
                        var propertyMatch = CreatePropertyMatch(memberAddress, listing, matchResult);
                        matches.Add(propertyMatch);
                    }
                }
            }

            _logger.LogInformation("Found {MatchCount} matches for institution {InstitutionId}", 
                matches.Count, institutionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding matches for institution {InstitutionId}", institutionId);
            throw;
        }

        return matches;
    }

    public MatchResult CalculateMatch(MemberAddress memberAddress, PropertyListing propertyListing)
    {
        var result = new MatchResult();

        try
        {
            // 1. Exact address match (highest priority)
            if (string.Equals(memberAddress.GetFullAddress(), propertyListing.GetFullAddress(), StringComparison.OrdinalIgnoreCase))
            {
                result.IsMatch = true;
                result.Confidence = MatchConfidence.Exact;
                result.Score = ExactMatchThreshold;
                result.Method = MatchMethod.ExactAddress;
                result.Details = "Exact address match";
                return result;
            }

            // 2. Normalized address match
            if (!string.IsNullOrEmpty(memberAddress.NormalizedAddress) && 
                !string.IsNullOrEmpty(propertyListing.NormalizedAddress))
            {
                if (string.Equals(memberAddress.NormalizedAddress, propertyListing.NormalizedAddress, StringComparison.OrdinalIgnoreCase))
                {
                    result.IsMatch = true;
                    result.Confidence = MatchConfidence.Exact;
                    result.Score = ExactMatchThreshold;
                    result.Method = MatchMethod.NormalizedAddress;
                    result.Details = "Normalized address match";
                    return result;
                }
            }

            // 3. Fuzzy string matching
            var fuzzyScore = Fuzz.Ratio(memberAddress.GetFullAddress(), propertyListing.GetFullAddress());
            
            if (fuzzyScore >= HighConfidenceThreshold)
            {
                result.IsMatch = true;
                result.Confidence = MatchConfidence.High;
                result.Score = fuzzyScore;
                result.Method = MatchMethod.FuzzyMatch;
                result.Details = $"High confidence fuzzy match (score: {fuzzyScore})";
                return result;
            }
            
            if (fuzzyScore >= MediumConfidenceThreshold)
            {
                result.IsMatch = true;
                result.Confidence = MatchConfidence.Medium;
                result.Score = fuzzyScore;
                result.Method = MatchMethod.FuzzyMatch;
                result.Details = $"Medium confidence fuzzy match (score: {fuzzyScore})";
                return result;
            }
            
            if (fuzzyScore >= LowConfidenceThreshold)
            {
                result.IsMatch = true;
                result.Confidence = MatchConfidence.Low;
                result.Score = fuzzyScore;
                result.Method = MatchMethod.FuzzyMatch;
                result.Details = $"Low confidence fuzzy match (score: {fuzzyScore})";
                return result;
            }

            // 4. Geographic proximity matching (if coordinates are available)
            if (memberAddress.Latitude.HasValue && memberAddress.Longitude.HasValue &&
                propertyListing.Latitude.HasValue && propertyListing.Longitude.HasValue)
            {
                var distance = CalculateDistance(
                    memberAddress.Latitude.Value, memberAddress.Longitude.Value,
                    propertyListing.Latitude.Value, propertyListing.Longitude.Value);

                if (distance <= GeographicProximityThreshold)
                {
                    result.IsMatch = true;
                    result.Confidence = MatchConfidence.Medium;
                    result.Score = Math.Max(0, 100 - (distance * 100)); // Convert distance to score
                    result.Method = MatchMethod.GeographicProximity;
                    result.Details = $"Geographic proximity match (distance: {distance:F3} miles)";
                    return result;
                }
            }

            // No match found
            result.IsMatch = false;
            result.Confidence = MatchConfidence.Low;
            result.Score = fuzzyScore;
            result.Method = MatchMethod.FuzzyMatch;
            result.Details = $"No match found (best score: {fuzzyScore})";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating match between member address {MemberAddressId} and listing {ListingId}", 
                memberAddress.Id, propertyListing.Id);
            
            result.IsMatch = false;
            result.Details = $"Error during matching: {ex.Message}";
        }

        return result;
    }

    private Task<IEnumerable<MemberAddress>> GetMemberAddressesByLocation(string state, string city)
    {
        // For efficiency, we could implement a location-based query in CosmosDbService
        // For now, we'll get all member addresses and filter (not ideal for production)
        
        // This is a simplified approach - in production, you'd want to optimize this
        // by implementing location-based queries in the CosmosDbService
        try
        {
            // Get unique institution IDs first, then get addresses for each
            // This is a workaround since we don't have a direct location-based query
            // In production, you'd implement this more efficiently
            
            // For now, return empty collection and log warning
            _logger.LogWarning("GetMemberAddressesByLocation not fully implemented - returning empty collection");
            return Task.FromResult<IEnumerable<MemberAddress>>(new List<MemberAddress>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting member addresses by location {State}, {City}", state, city);
            return Task.FromResult<IEnumerable<MemberAddress>>(new List<MemberAddress>());
        }
    }

    private PropertyMatch CreatePropertyMatch(MemberAddress memberAddress, PropertyListing propertyListing, MatchResult matchResult)
    {
        return new PropertyMatch
        {
            MemberAddressId = memberAddress.Id,
            PropertyListingId = propertyListing.Id,
            InstitutionId = memberAddress.InstitutionId,
            AnonymousReferenceId = memberAddress.AnonymousReferenceId,
            MatchConfidence = matchResult.Confidence,
            MatchScore = matchResult.Score,
            MatchMethod = matchResult.Method,
            MemberAddress = memberAddress.GetFullAddress(),
            ListingAddress = propertyListing.GetFullAddress(),
            ListingPrice = propertyListing.Price,
            ListingDate = propertyListing.ListingDate,
            PropertyStatus = propertyListing.Status
        };
    }

    private static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        // Haversine formula to calculate distance between two points on Earth
        const double R = 3959; // Earth's radius in miles

        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        var distance = R * c;

        return distance;
    }

    private static double ToRadians(double degrees)
    {
        return degrees * (Math.PI / 180);
    }
}
