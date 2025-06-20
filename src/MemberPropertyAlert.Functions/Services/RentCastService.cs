using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using MemberPropertyAlert.Core.Models;
using MemberPropertyAlert.Core.Services;

namespace MemberPropertyAlert.Functions.Services
{
    public class RentCastService : IRentCastService
    {
        private readonly HttpClient _httpClient;
        private readonly RentCastConfiguration _config;
        private readonly ILogger<RentCastService> _logger;

        public RentCastService(
            HttpClient httpClient,
            IOptions<RentCastConfiguration> config,
            ILogger<RentCastService> logger)
        {
            _httpClient = httpClient;
            _config = config.Value;
            _logger = logger;
        }

        public async Task<PropertyListingResult> GetPropertyListingStatusAsync(string address)
        {
            _logger.LogInformation("Checking property listing status for address: {Address}", address);

            try
            {
                // RentCast API endpoint for property details
                var endpoint = $"/properties?address={Uri.EscapeDataString(address)}";
                var response = await _httpClient.GetAsync(endpoint);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("RentCast API returned {StatusCode} for address {Address}", 
                        response.StatusCode, address);
                    
                    return new PropertyListingResult
                    {
                        IsSuccess = false,
                        ErrorMessage = $"API returned {response.StatusCode}",
                        OriginalAddress = address,
                        Status = PropertyStatus.Unknown
                    };
                }

                var content = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<RentCastApiResponse>(content, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                return ProcessApiResponse(apiResponse, address);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking property listing status for address {Address}", address);
                
                return new PropertyListingResult
                {
                    IsSuccess = false,
                    ErrorMessage = ex.Message,
                    OriginalAddress = address,
                    Status = PropertyStatus.Unknown
                };
            }
        }

        public async Task<PropertyListingResult> GetPropertyListingStatusAsync(string streetAddress, string city, string state, string zipCode)
        {
            var fullAddress = $"{streetAddress}, {city}, {state} {zipCode}";
            return await GetPropertyListingStatusAsync(fullAddress);
        }

        public async Task<List<PropertyListingResult>> GetBulkPropertyListingStatusAsync(List<string> addresses)
        {
            _logger.LogInformation("Checking bulk property listing status for {Count} addresses", addresses.Count);

            var results = new List<PropertyListingResult>();
            var semaphore = new SemaphoreSlim(5, 5); // Limit concurrent requests

            var tasks = addresses.Select(async address =>
            {
                await semaphore.WaitAsync();
                try
                {
                    // Add delay to respect rate limits
                    await Task.Delay(_config.RateLimitDelayMs);
                    return await GetPropertyListingStatusAsync(address);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            results.AddRange(await Task.WhenAll(tasks));
            return results;
        }

        public async Task<bool> ValidateApiKeyAsync()
        {
            try
            {
                _logger.LogInformation("Validating RentCast API key");
                
                // Test with a simple endpoint
                var response = await _httpClient.GetAsync("/properties?address=123%20Main%20St,%20Anytown,%20CA%2012345");
                
                // Even if the property doesn't exist, a valid API key should return 200 or 404, not 401/403
                var isValid = response.StatusCode != System.Net.HttpStatusCode.Unauthorized && 
                             response.StatusCode != System.Net.HttpStatusCode.Forbidden;

                _logger.LogInformation("API key validation result: {IsValid}", isValid);
                return isValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating API key");
                return false;
            }
        }

        public async Task<PropertyListing?> GetPropertyListingAsync(string address)
        {
            _logger.LogInformation("Getting property listing for address: {Address}", address);

            try
            {
                var endpoint = $"/properties?address={Uri.EscapeDataString(address)}";
                var response = await _httpClient.GetAsync(endpoint);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("RentCast API returned {StatusCode} for address {Address}", 
                        response.StatusCode, address);
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<RentCastApiResponse>(content, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                if (apiResponse?.Properties == null || !apiResponse.Properties.Any())
                {
                    return null;
                }

                var property = apiResponse.Properties.First();
                return ConvertToPropertyListing(property);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting property listing for address {Address}", address);
                return null;
            }
        }

        public async Task<bool> IsPropertyListedAsync(string address)
        {
            var listing = await GetPropertyListingAsync(address);
            return listing?.IsActive == true;
        }

        public async Task<PropertyListing[]> GetRecentListingsAsync(string city, string state, int daysBack = 7)
        {
            _logger.LogInformation("Getting recent listings for {City}, {State} (last {Days} days)", city, state, daysBack);

            try
            {
                var endpoint = $"/listings/sale?city={Uri.EscapeDataString(city)}&state={Uri.EscapeDataString(state)}&daysBack={daysBack}";
                var response = await _httpClient.GetAsync(endpoint);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("RentCast API returned {StatusCode} for recent listings", response.StatusCode);
                    return Array.Empty<PropertyListing>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var listings = JsonSerializer.Deserialize<List<PropertyListing>>(content, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                return listings?.ToArray() ?? Array.Empty<PropertyListing>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent listings for {City}, {State}", city, state);
                return Array.Empty<PropertyListing>();
            }
        }

        public async Task<PropertyListing[]> GetStateListingsAsync(string state)
        {
            _logger.LogInformation("Getting all listings for state: {State}", state);

            try
            {
                var endpoint = $"/listings/sale?state={Uri.EscapeDataString(state)}";
                var response = await _httpClient.GetAsync(endpoint);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("RentCast API returned {StatusCode} for state listings", response.StatusCode);
                    return Array.Empty<PropertyListing>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var listings = JsonSerializer.Deserialize<List<PropertyListing>>(content, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                return listings?.ToArray() ?? Array.Empty<PropertyListing>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting state listings for {State}", state);
                return Array.Empty<PropertyListing>();
            }
        }

        public async Task<PropertyListing[]> GetNewListingsAsync(string state, DateTime since)
        {
            _logger.LogInformation("Getting new listings for state {State} since {Since}", state, since);

            try
            {
                var sinceParam = since.ToString("yyyy-MM-dd");
                var endpoint = $"/listings/sale?state={Uri.EscapeDataString(state)}&listedSince={sinceParam}";
                var response = await _httpClient.GetAsync(endpoint);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("RentCast API returned {StatusCode} for new listings", response.StatusCode);
                    return Array.Empty<PropertyListing>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var listings = JsonSerializer.Deserialize<List<PropertyListing>>(content, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                return listings?.ToArray() ?? Array.Empty<PropertyListing>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting new listings for {State} since {Since}", state, since);
                return Array.Empty<PropertyListing>();
            }
        }

        private PropertyListingResult ProcessApiResponse(RentCastApiResponse? apiResponse, string originalAddress)
        {
            if (apiResponse?.Properties == null || !apiResponse.Properties.Any())
            {
                return new PropertyListingResult
                {
                    IsSuccess = true,
                    OriginalAddress = originalAddress,
                    Status = PropertyStatus.NotListed,
                    ErrorMessage = "No property data found"
                };
            }

            var property = apiResponse.Properties.First();
            var status = DeterminePropertyStatus(property);
            var listingDetails = CreateListingDetails(property);

            return new PropertyListingResult
            {
                IsSuccess = true,
                OriginalAddress = originalAddress,
                NormalizedAddress = property.FormattedAddress,
                Status = status,
                ListingDetails = listingDetails,
                RawData = new Dictionary<string, object>
                {
                    ["rentcast_property"] = property
                }
            };
        }

        private PropertyStatus DeterminePropertyStatus(RentCastProperty property)
        {
            // RentCast API logic to determine if property is listed for sale
            if (property.ForSale?.IsListed == true)
            {
                return property.ForSale.Status?.ToLower() switch
                {
                    "active" => PropertyStatus.Listed,
                    "under contract" => PropertyStatus.UnderContract,
                    "sold" => PropertyStatus.Sold,
                    "off market" => PropertyStatus.OffMarket,
                    _ => PropertyStatus.Listed
                };
            }

            return PropertyStatus.NotListed;
        }

        private PropertyListingDetails? CreateListingDetails(RentCastProperty property)
        {
            if (property.ForSale?.IsListed != true)
                return null;

            return new PropertyListingDetails
            {
                ListPrice = property.ForSale.Price?.ToString(),
                ListedDate = property.ForSale.ListDate,
                DaysOnMarket = property.ForSale.DaysOnMarket,
                ListingAgent = property.ForSale.ListingAgent,
                ListingAgentPhone = property.ForSale.ListingAgentPhone,
                ListingUrl = property.ForSale.ListingUrl,
                PropertyType = property.PropertyType,
                Bedrooms = property.Bedrooms,
                Bathrooms = property.Bathrooms,
                SquareFeet = property.SquareFeet,
                Description = property.Description,
                Photos = property.Photos ?? new List<string>(),
                AdditionalData = new Dictionary<string, object>
                {
                    ["mls_id"] = property.ForSale.MlsId ?? string.Empty,
                    ["lot_size"] = property.LotSize ?? 0,
                    ["year_built"] = property.YearBuilt ?? 0
                }
            };
        }

        private PropertyListing ConvertToPropertyListing(RentCastProperty property)
        {
            return new PropertyListing
            {
                Id = property.Id ?? Guid.NewGuid().ToString(),
                Address = ExtractAddress(property.FormattedAddress),
                City = ExtractCity(property.FormattedAddress),
                State = ExtractState(property.FormattedAddress),
                ZipCode = ExtractZipCode(property.FormattedAddress),
                Price = property.ForSale?.Price,
                Bedrooms = property.Bedrooms,
                Bathrooms = property.Bathrooms,
                SquareFootage = property.SquareFeet,
                ListingDate = property.ForSale?.ListDate,
                DaysOnMarket = property.ForSale?.DaysOnMarket,
                PropertyType = property.PropertyType ?? string.Empty,
                MlsNumber = property.ForSale?.MlsId ?? string.Empty,
                ListingAgent = property.ForSale?.ListingAgent ?? string.Empty,
                Description = property.Description ?? string.Empty,
                Photos = property.Photos ?? new List<string>(),
                Status = property.ForSale?.Status ?? "unknown",
                LastUpdated = DateTime.UtcNow
            };
        }

        private string ExtractAddress(string? formattedAddress)
        {
            if (string.IsNullOrEmpty(formattedAddress)) return string.Empty;
            var parts = formattedAddress.Split(',');
            return parts.Length > 0 ? parts[0].Trim() : string.Empty;
        }

        private string ExtractCity(string? formattedAddress)
        {
            if (string.IsNullOrEmpty(formattedAddress)) return string.Empty;
            var parts = formattedAddress.Split(',');
            return parts.Length > 1 ? parts[1].Trim() : string.Empty;
        }

        private string ExtractState(string? formattedAddress)
        {
            if (string.IsNullOrEmpty(formattedAddress)) return string.Empty;
            var parts = formattedAddress.Split(',');
            if (parts.Length > 2)
            {
                var stateZip = parts[2].Trim().Split(' ');
                return stateZip.Length > 0 ? stateZip[0].Trim() : string.Empty;
            }
            return string.Empty;
        }

        private string ExtractZipCode(string? formattedAddress)
        {
            if (string.IsNullOrEmpty(formattedAddress)) return string.Empty;
            var parts = formattedAddress.Split(',');
            if (parts.Length > 2)
            {
                var stateZip = parts[2].Trim().Split(' ');
                return stateZip.Length > 1 ? stateZip[1].Trim() : string.Empty;
            }
            return string.Empty;
        }
    }

    // RentCast API Response Models
    public class RentCastApiResponse
    {
        public List<RentCastProperty>? Properties { get; set; }
        public int Count { get; set; }
        public string? Status { get; set; }
    }

    public class RentCastProperty
    {
        public string? Id { get; set; }
        public string? FormattedAddress { get; set; }
        public string? PropertyType { get; set; }
        public int? Bedrooms { get; set; }
        public int? Bathrooms { get; set; }
        public int? SquareFeet { get; set; }
        public int? LotSize { get; set; }
        public int? YearBuilt { get; set; }
        public string? Description { get; set; }
        public List<string>? Photos { get; set; }
        public RentCastForSaleInfo? ForSale { get; set; }
    }

    public class RentCastForSaleInfo
    {
        public bool IsListed { get; set; }
        public decimal? Price { get; set; }
        public string? Status { get; set; }
        public DateTime? ListDate { get; set; }
        public int? DaysOnMarket { get; set; }
        public string? MlsId { get; set; }
        public string? ListingAgent { get; set; }
        public string? ListingAgentPhone { get; set; }
        public string? ListingUrl { get; set; }
    }
}
