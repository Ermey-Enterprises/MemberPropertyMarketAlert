using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MemberPropertyMarketAlert.Core.Models;

namespace MemberPropertyMarketAlert.Core.Services;

public class CosmosDbService : ICosmosDbService
{
    private readonly CosmosClient _cosmosClient;
    private readonly Database _database;
    private readonly Container _memberAddressContainer;
    private readonly Container _propertyListingContainer;
    private readonly Container _propertyMatchContainer;
    private readonly ILogger<CosmosDbService> _logger;

    public CosmosDbService(CosmosClient cosmosClient, IConfiguration configuration, ILogger<CosmosDbService> logger)
    {
        _cosmosClient = cosmosClient;
        _logger = logger;
        
        var databaseName = configuration["CosmosDb:DatabaseName"] ?? "MemberPropertyMarketAlert";
        _database = _cosmosClient.GetDatabase(databaseName);
        
        _memberAddressContainer = _database.GetContainer("MemberAddresses");
        _propertyListingContainer = _database.GetContainer("PropertyListings");
        _propertyMatchContainer = _database.GetContainer("PropertyMatches");
    }

    #region Member Address Operations

    public async Task<MemberAddress> CreateMemberAddressAsync(MemberAddress memberAddress)
    {
        try
        {
            memberAddress.UpdateNormalizedAddress();
            var response = await _memberAddressContainer.CreateItemAsync(memberAddress, new PartitionKey(memberAddress.PartitionKey));
            _logger.LogInformation("Created member address with ID: {Id}", memberAddress.Id);
            return response.Resource;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating member address with ID: {Id}", memberAddress.Id);
            throw;
        }
    }

    public async Task<MemberAddress?> GetMemberAddressAsync(string id, string partitionKey)
    {
        try
        {
            var response = await _memberAddressContainer.ReadItemAsync<MemberAddress>(id, new PartitionKey(partitionKey));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving member address with ID: {Id}", id);
            throw;
        }
    }

    public async Task<IEnumerable<MemberAddress>> GetMemberAddressesByInstitutionAsync(string institutionId)
    {
        try
        {
            var query = new QueryDefinition("SELECT * FROM c WHERE c.institutionId = @institutionId AND c.isActive = true")
                .WithParameter("@institutionId", institutionId);

            var iterator = _memberAddressContainer.GetItemQueryIterator<MemberAddress>(query);
            var results = new List<MemberAddress>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response);
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving member addresses for institution: {InstitutionId}", institutionId);
            throw;
        }
    }

    public async Task<MemberAddress> UpdateMemberAddressAsync(MemberAddress memberAddress)
    {
        try
        {
            memberAddress.UpdatedDate = DateTime.UtcNow;
            memberAddress.UpdateNormalizedAddress();
            var response = await _memberAddressContainer.ReplaceItemAsync(memberAddress, memberAddress.Id, new PartitionKey(memberAddress.PartitionKey));
            _logger.LogInformation("Updated member address with ID: {Id}", memberAddress.Id);
            return response.Resource;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating member address with ID: {Id}", memberAddress.Id);
            throw;
        }
    }

    public async Task DeleteMemberAddressAsync(string id, string partitionKey)
    {
        try
        {
            await _memberAddressContainer.DeleteItemAsync<MemberAddress>(id, new PartitionKey(partitionKey));
            _logger.LogInformation("Deleted member address with ID: {Id}", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting member address with ID: {Id}", id);
            throw;
        }
    }

    #endregion

    #region Property Listing Operations

    public async Task<PropertyListing> CreatePropertyListingAsync(PropertyListing propertyListing)
    {
        try
        {
            propertyListing.UpdateNormalizedAddress();
            var response = await _propertyListingContainer.CreateItemAsync(propertyListing, new PartitionKey(propertyListing.PartitionKey));
            _logger.LogInformation("Created property listing with ID: {Id}", propertyListing.Id);
            return response.Resource;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating property listing with ID: {Id}", propertyListing.Id);
            throw;
        }
    }

    public async Task<PropertyListing?> GetPropertyListingAsync(string id, string partitionKey)
    {
        try
        {
            var response = await _propertyListingContainer.ReadItemAsync<PropertyListing>(id, new PartitionKey(partitionKey));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving property listing with ID: {Id}", id);
            throw;
        }
    }

    public async Task<IEnumerable<PropertyListing>> GetActivePropertyListingsAsync()
    {
        try
        {
            var query = new QueryDefinition("SELECT * FROM c WHERE c.status = @status")
                .WithParameter("@status", PropertyStatus.Active.ToString());

            var iterator = _propertyListingContainer.GetItemQueryIterator<PropertyListing>(query);
            var results = new List<PropertyListing>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response);
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active property listings");
            throw;
        }
    }

    public async Task<IEnumerable<PropertyListing>> GetPropertyListingsByLocationAsync(string state, string city)
    {
        try
        {
            var query = new QueryDefinition("SELECT * FROM c WHERE c.state = @state AND c.city = @city AND c.status = @status")
                .WithParameter("@state", state)
                .WithParameter("@city", city)
                .WithParameter("@status", PropertyStatus.Active.ToString());

            var iterator = _propertyListingContainer.GetItemQueryIterator<PropertyListing>(query);
            var results = new List<PropertyListing>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response);
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving property listings for location: {State}, {City}", state, city);
            throw;
        }
    }

    public async Task<PropertyListing> UpdatePropertyListingAsync(PropertyListing propertyListing)
    {
        try
        {
            propertyListing.UpdatedDate = DateTime.UtcNow;
            propertyListing.UpdateNormalizedAddress();
            var response = await _propertyListingContainer.ReplaceItemAsync(propertyListing, propertyListing.Id, new PartitionKey(propertyListing.PartitionKey));
            _logger.LogInformation("Updated property listing with ID: {Id}", propertyListing.Id);
            return response.Resource;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating property listing with ID: {Id}", propertyListing.Id);
            throw;
        }
    }

    public async Task DeletePropertyListingAsync(string id, string partitionKey)
    {
        try
        {
            await _propertyListingContainer.DeleteItemAsync<PropertyListing>(id, new PartitionKey(partitionKey));
            _logger.LogInformation("Deleted property listing with ID: {Id}", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting property listing with ID: {Id}", id);
            throw;
        }
    }

    #endregion

    #region Property Match Operations

    public async Task<PropertyMatch> CreatePropertyMatchAsync(PropertyMatch propertyMatch)
    {
        try
        {
            var response = await _propertyMatchContainer.CreateItemAsync(propertyMatch, new PartitionKey(propertyMatch.PartitionKey));
            _logger.LogInformation("Created property match with ID: {Id}", propertyMatch.Id);
            return response.Resource;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating property match with ID: {Id}", propertyMatch.Id);
            throw;
        }
    }

    public async Task<PropertyMatch?> GetPropertyMatchAsync(string id, string partitionKey)
    {
        try
        {
            var response = await _propertyMatchContainer.ReadItemAsync<PropertyMatch>(id, new PartitionKey(partitionKey));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving property match with ID: {Id}", id);
            throw;
        }
    }

    public async Task<IEnumerable<PropertyMatch>> GetPropertyMatchesByInstitutionAsync(string institutionId)
    {
        try
        {
            var query = new QueryDefinition("SELECT * FROM c WHERE c.institutionId = @institutionId")
                .WithParameter("@institutionId", institutionId);

            var iterator = _propertyMatchContainer.GetItemQueryIterator<PropertyMatch>(query);
            var results = new List<PropertyMatch>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response);
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving property matches for institution: {InstitutionId}", institutionId);
            throw;
        }
    }

    public async Task<IEnumerable<PropertyMatch>> GetUnprocessedPropertyMatchesAsync()
    {
        try
        {
            var query = new QueryDefinition("SELECT * FROM c WHERE c.isProcessed = false");

            var iterator = _propertyMatchContainer.GetItemQueryIterator<PropertyMatch>(query);
            var results = new List<PropertyMatch>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response);
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving unprocessed property matches");
            throw;
        }
    }

    public async Task<PropertyMatch> UpdatePropertyMatchAsync(PropertyMatch propertyMatch)
    {
        try
        {
            propertyMatch.UpdatedDate = DateTime.UtcNow;
            var response = await _propertyMatchContainer.ReplaceItemAsync(propertyMatch, propertyMatch.Id, new PartitionKey(propertyMatch.PartitionKey));
            _logger.LogInformation("Updated property match with ID: {Id}", propertyMatch.Id);
            return response.Resource;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating property match with ID: {Id}", propertyMatch.Id);
            throw;
        }
    }

    public async Task DeletePropertyMatchAsync(string id, string partitionKey)
    {
        try
        {
            await _propertyMatchContainer.DeleteItemAsync<PropertyMatch>(id, new PartitionKey(partitionKey));
            _logger.LogInformation("Deleted property match with ID: {Id}", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting property match with ID: {Id}", id);
            throw;
        }
    }

    #endregion

    #region Bulk Operations

    public async Task<IEnumerable<MemberAddress>> CreateMemberAddressesBulkAsync(IEnumerable<MemberAddress> memberAddresses)
    {
        var results = new List<MemberAddress>();
        var tasks = new List<Task<MemberAddress>>();

        foreach (var memberAddress in memberAddresses)
        {
            tasks.Add(CreateMemberAddressAsync(memberAddress));
        }

        try
        {
            var completedTasks = await Task.WhenAll(tasks);
            results.AddRange(completedTasks);
            _logger.LogInformation("Bulk created {Count} member addresses", results.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in bulk creating member addresses");
            throw;
        }

        return results;
    }

    public async Task<IEnumerable<PropertyListing>> CreatePropertyListingsBulkAsync(IEnumerable<PropertyListing> propertyListings)
    {
        var results = new List<PropertyListing>();
        var tasks = new List<Task<PropertyListing>>();

        foreach (var propertyListing in propertyListings)
        {
            tasks.Add(CreatePropertyListingAsync(propertyListing));
        }

        try
        {
            var completedTasks = await Task.WhenAll(tasks);
            results.AddRange(completedTasks);
            _logger.LogInformation("Bulk created {Count} property listings", results.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in bulk creating property listings");
            throw;
        }

        return results;
    }

    #endregion
}
