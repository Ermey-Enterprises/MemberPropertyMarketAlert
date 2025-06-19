using MemberPropertyMarketAlert.Core.Models;
using System.Collections.Concurrent;

namespace MemberPropertyMarketAlert.Core.Services;

public class InMemoryCosmosDbService : ICosmosDbService
{
    private readonly ConcurrentDictionary<string, MemberAddress> _memberAddresses = new();
    private readonly ConcurrentDictionary<string, PropertyListing> _propertyListings = new();
    private readonly ConcurrentDictionary<string, PropertyMatch> _propertyMatches = new();

    public Task<MemberAddress> CreateMemberAddressAsync(MemberAddress memberAddress)
    {
        memberAddress.Id = Guid.NewGuid().ToString();
        memberAddress.CreatedDate = DateTime.UtcNow;
        memberAddress.UpdatedDate = DateTime.UtcNow;
        memberAddress.UpdateNormalizedAddress();
        
        _memberAddresses[memberAddress.Id] = memberAddress;
        return Task.FromResult(memberAddress);
    }

    public Task<IEnumerable<MemberAddress>> CreateMemberAddressesBulkAsync(IEnumerable<MemberAddress> memberAddresses)
    {
        var createdAddresses = new List<MemberAddress>();
        
        foreach (var address in memberAddresses)
        {
            address.Id = Guid.NewGuid().ToString();
            address.CreatedDate = DateTime.UtcNow;
            address.UpdatedDate = DateTime.UtcNow;
            address.UpdateNormalizedAddress();
            
            _memberAddresses[address.Id] = address;
            createdAddresses.Add(address);
        }
        
        return Task.FromResult<IEnumerable<MemberAddress>>(createdAddresses);
    }

    public Task<MemberAddress?> GetMemberAddressAsync(string id, string partitionKey)
    {
        if (_memberAddresses.TryGetValue(id, out var address) && address.InstitutionId == partitionKey)
        {
            return Task.FromResult<MemberAddress?>(address);
        }
        return Task.FromResult<MemberAddress?>(null);
    }

    public Task<IEnumerable<MemberAddress>> GetMemberAddressesByInstitutionAsync(string institutionId)
    {
        var addresses = _memberAddresses.Values
            .Where(a => a.InstitutionId == institutionId && a.IsActive)
            .ToList();
        
        return Task.FromResult<IEnumerable<MemberAddress>>(addresses);
    }

    public Task<MemberAddress> UpdateMemberAddressAsync(MemberAddress memberAddress)
    {
        memberAddress.UpdatedDate = DateTime.UtcNow;
        memberAddress.UpdateNormalizedAddress();
        
        _memberAddresses[memberAddress.Id] = memberAddress;
        return Task.FromResult(memberAddress);
    }

    public Task DeleteMemberAddressAsync(string id, string partitionKey)
    {
        if (_memberAddresses.TryGetValue(id, out var address) && address.InstitutionId == partitionKey)
        {
            _memberAddresses.TryRemove(id, out _);
        }
        return Task.CompletedTask;
    }

    public Task<PropertyListing> CreatePropertyListingAsync(PropertyListing propertyListing)
    {
        propertyListing.Id = Guid.NewGuid().ToString();
        propertyListing.CreatedDate = DateTime.UtcNow;
        propertyListing.UpdatedDate = DateTime.UtcNow;
        propertyListing.UpdateNormalizedAddress();
        
        _propertyListings[propertyListing.Id] = propertyListing;
        return Task.FromResult(propertyListing);
    }

    public Task<PropertyListing?> GetPropertyListingAsync(string id, string partitionKey)
    {
        _propertyListings.TryGetValue(id, out var listing);
        return Task.FromResult(listing);
    }

    public Task<IEnumerable<PropertyListing>> GetActivePropertyListingsAsync()
    {
        var listings = _propertyListings.Values
            .Where(l => l.Status == PropertyStatus.Active)
            .ToList();
        
        return Task.FromResult<IEnumerable<PropertyListing>>(listings);
    }

    public Task<IEnumerable<PropertyListing>> GetPropertyListingsByLocationAsync(string state, string city)
    {
        var listings = _propertyListings.Values
            .Where(l => l.State == state && l.City == city)
            .ToList();
        
        return Task.FromResult<IEnumerable<PropertyListing>>(listings);
    }

    public Task<PropertyListing> UpdatePropertyListingAsync(PropertyListing propertyListing)
    {
        propertyListing.UpdatedDate = DateTime.UtcNow;
        propertyListing.UpdateNormalizedAddress();
        
        _propertyListings[propertyListing.Id] = propertyListing;
        return Task.FromResult(propertyListing);
    }

    public Task DeletePropertyListingAsync(string id, string partitionKey)
    {
        _propertyListings.TryRemove(id, out _);
        return Task.CompletedTask;
    }

    public Task<IEnumerable<PropertyListing>> CreatePropertyListingsBulkAsync(IEnumerable<PropertyListing> propertyListings)
    {
        var createdListings = new List<PropertyListing>();
        
        foreach (var listing in propertyListings)
        {
            listing.Id = Guid.NewGuid().ToString();
            listing.CreatedDate = DateTime.UtcNow;
            listing.UpdatedDate = DateTime.UtcNow;
            listing.UpdateNormalizedAddress();
            
            _propertyListings[listing.Id] = listing;
            createdListings.Add(listing);
        }
        
        return Task.FromResult<IEnumerable<PropertyListing>>(createdListings);
    }

    public Task<PropertyMatch> CreatePropertyMatchAsync(PropertyMatch propertyMatch)
    {
        propertyMatch.Id = Guid.NewGuid().ToString();
        propertyMatch.CreatedDate = DateTime.UtcNow;
        propertyMatch.UpdatedDate = DateTime.UtcNow;
        
        _propertyMatches[propertyMatch.Id] = propertyMatch;
        return Task.FromResult(propertyMatch);
    }

    public Task<PropertyMatch?> GetPropertyMatchAsync(string id, string partitionKey)
    {
        _propertyMatches.TryGetValue(id, out var match);
        return Task.FromResult(match);
    }

    public Task<IEnumerable<PropertyMatch>> GetPropertyMatchesByInstitutionAsync(string institutionId)
    {
        var matches = _propertyMatches.Values
            .Where(m => m.InstitutionId == institutionId)
            .ToList();
        
        return Task.FromResult<IEnumerable<PropertyMatch>>(matches);
    }

    public Task<IEnumerable<PropertyMatch>> GetUnprocessedPropertyMatchesAsync()
    {
        var matches = _propertyMatches.Values
            .Where(m => !m.IsProcessed)
            .ToList();
        
        return Task.FromResult<IEnumerable<PropertyMatch>>(matches);
    }

    public Task<PropertyMatch> UpdatePropertyMatchAsync(PropertyMatch propertyMatch)
    {
        propertyMatch.UpdatedDate = DateTime.UtcNow;
        
        _propertyMatches[propertyMatch.Id] = propertyMatch;
        return Task.FromResult(propertyMatch);
    }

    public Task DeletePropertyMatchAsync(string id, string partitionKey)
    {
        _propertyMatches.TryRemove(id, out _);
        return Task.CompletedTask;
    }
}
