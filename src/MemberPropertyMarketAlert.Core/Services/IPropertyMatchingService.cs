using MemberPropertyMarketAlert.Core.Models;

namespace MemberPropertyMarketAlert.Core.Services;

public interface IPropertyMatchingService
{
    /// <summary>
    /// Finds potential matches between member addresses and property listings
    /// </summary>
    /// <param name="propertyListings">Collection of property listings to match against</param>
    /// <returns>Collection of property matches found</returns>
    Task<IEnumerable<PropertyMatch>> FindMatchesAsync(IEnumerable<PropertyListing> propertyListings);

    /// <summary>
    /// Finds matches for a specific property listing against all member addresses
    /// </summary>
    /// <param name="propertyListing">The property listing to match</param>
    /// <returns>Collection of property matches found</returns>
    Task<IEnumerable<PropertyMatch>> FindMatchesForListingAsync(PropertyListing propertyListing);

    /// <summary>
    /// Finds matches for a specific institution's member addresses
    /// </summary>
    /// <param name="institutionId">The institution ID to match for</param>
    /// <param name="propertyListings">Collection of property listings to match against</param>
    /// <returns>Collection of property matches found</returns>
    Task<IEnumerable<PropertyMatch>> FindMatchesForInstitutionAsync(string institutionId, IEnumerable<PropertyListing> propertyListings);

    /// <summary>
    /// Calculates match confidence and score between a member address and property listing
    /// </summary>
    /// <param name="memberAddress">The member address</param>
    /// <param name="propertyListing">The property listing</param>
    /// <returns>Match result with confidence and score</returns>
    MatchResult CalculateMatch(MemberAddress memberAddress, PropertyListing propertyListing);
}

public class MatchResult
{
    public bool IsMatch { get; set; }
    public MatchConfidence Confidence { get; set; }
    public double Score { get; set; }
    public MatchMethod Method { get; set; }
    public string? Details { get; set; }
}
