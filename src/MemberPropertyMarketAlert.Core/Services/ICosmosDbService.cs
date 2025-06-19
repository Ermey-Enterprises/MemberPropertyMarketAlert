using MemberPropertyMarketAlert.Core.Models;

namespace MemberPropertyMarketAlert.Core.Services;

public interface ICosmosDbService
{
    // Member Address operations
    Task<MemberAddress> CreateMemberAddressAsync(MemberAddress memberAddress);
    Task<MemberAddress?> GetMemberAddressAsync(string id, string partitionKey);
    Task<IEnumerable<MemberAddress>> GetMemberAddressesByInstitutionAsync(string institutionId);
    Task<MemberAddress> UpdateMemberAddressAsync(MemberAddress memberAddress);
    Task DeleteMemberAddressAsync(string id, string partitionKey);

    // Property Listing operations
    Task<PropertyListing> CreatePropertyListingAsync(PropertyListing propertyListing);
    Task<PropertyListing?> GetPropertyListingAsync(string id, string partitionKey);
    Task<IEnumerable<PropertyListing>> GetActivePropertyListingsAsync();
    Task<IEnumerable<PropertyListing>> GetPropertyListingsByLocationAsync(string state, string city);
    Task<PropertyListing> UpdatePropertyListingAsync(PropertyListing propertyListing);
    Task DeletePropertyListingAsync(string id, string partitionKey);

    // Property Match operations
    Task<PropertyMatch> CreatePropertyMatchAsync(PropertyMatch propertyMatch);
    Task<PropertyMatch?> GetPropertyMatchAsync(string id, string partitionKey);
    Task<IEnumerable<PropertyMatch>> GetPropertyMatchesByInstitutionAsync(string institutionId);
    Task<IEnumerable<PropertyMatch>> GetUnprocessedPropertyMatchesAsync();
    Task<PropertyMatch> UpdatePropertyMatchAsync(PropertyMatch propertyMatch);
    Task DeletePropertyMatchAsync(string id, string partitionKey);

    // Bulk operations
    Task<IEnumerable<MemberAddress>> CreateMemberAddressesBulkAsync(IEnumerable<MemberAddress> memberAddresses);
    Task<IEnumerable<PropertyListing>> CreatePropertyListingsBulkAsync(IEnumerable<PropertyListing> propertyListings);
}
