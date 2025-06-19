using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using MemberPropertyMarketAlert.Core.Services;

namespace MemberPropertyMarketAlert.Functions.Functions;

public class PropertyMatchingFunction
{
    private readonly ICosmosDbService _cosmosDbService;
    private readonly IPropertyMatchingService _propertyMatchingService;
    private readonly ILogger<PropertyMatchingFunction> _logger;

    public PropertyMatchingFunction(
        ICosmosDbService cosmosDbService,
        IPropertyMatchingService propertyMatchingService,
        ILogger<PropertyMatchingFunction> logger)
    {
        _cosmosDbService = cosmosDbService;
        _propertyMatchingService = propertyMatchingService;
        _logger = logger;
    }

    [Function("ProcessPropertyMatching")]
    public async Task ProcessPropertyMatching([TimerTrigger("0 */15 * * * *")] TimerInfo myTimer)
    {
        _logger.LogInformation("Property matching function executed at: {Time}", DateTime.Now);

        try
        {
            // Get all active property listings
            var propertyListings = await _cosmosDbService.GetActivePropertyListingsAsync();
            _logger.LogInformation("Found {Count} active property listings to process", propertyListings.Count());

            if (!propertyListings.Any())
            {
                _logger.LogInformation("No active property listings found, skipping matching process");
                return;
            }

            // Find matches for all listings
            var matches = await _propertyMatchingService.FindMatchesAsync(propertyListings);
            _logger.LogInformation("Found {MatchCount} potential matches", matches.Count());

            // Save matches to database
            var savedMatches = 0;
            foreach (var match in matches)
            {
                try
                {
                    await _cosmosDbService.CreatePropertyMatchAsync(match);
                    savedMatches++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error saving property match for member {MemberAddressId} and listing {PropertyListingId}",
                        match.MemberAddressId, match.PropertyListingId);
                }
            }

            _logger.LogInformation("Successfully saved {SavedCount} out of {TotalCount} matches", savedMatches, matches.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during property matching process");
            throw;
        }

        _logger.LogInformation("Property matching function completed at: {Time}", DateTime.Now);
    }

    [Function("ProcessSingleListingMatching")]
    public Task ProcessSingleListingMatching(
        [ServiceBusTrigger("property-listings", Connection = "ServiceBusConnection")] string propertyListingId)
    {
        _logger.LogInformation("Processing single listing matching for: {PropertyListingId}", propertyListingId);

        try
        {
            // This would be triggered when a new property listing is added
            // The message would contain the property listing ID
            
            // For now, we'll implement a basic version
            // In a full implementation, you'd parse the message to get the listing details
            
            _logger.LogInformation("Single listing matching completed for: {PropertyListingId}", propertyListingId);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing single listing matching for: {PropertyListingId}", propertyListingId);
            throw;
        }
    }
}
