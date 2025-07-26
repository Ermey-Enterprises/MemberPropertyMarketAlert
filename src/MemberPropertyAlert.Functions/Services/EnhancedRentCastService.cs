using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using System.Net;
using Polly;
using Polly.CircuitBreaker;
using Polly.Extensions.Http;
using Polly.Timeout;
using MemberPropertyAlert.Core.Models;
using MemberPropertyAlert.Core.Services;
using MemberPropertyAlert.Functions.Configuration;

namespace MemberPropertyAlert.Functions.Services
{
    /// <summary>
    /// Enhanced RentCast service with resilience patterns, caching, and comprehensive error handling
    /// </summary>
    public class EnhancedRentCastService : IRentCastService
    {
        private readonly HttpClient _httpClient;
        private readonly ExtendedRentCastConfiguration _config;
        private readonly ILogger<EnhancedRentCastService> _logger;
        private readonly IMemoryCache _cache;
        private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;
        private readonly IAsyncPolicy<HttpResponseMessage> _circuitBreakerPolicy;
        private readonly IAsyncPolicy<HttpResponseMessage> _combinedPolicy;

        public EnhancedRentCastService(
            HttpClient httpClient,
            IOptions<ExtendedRentCastConfiguration> config,
            ILogger<EnhancedRentCastService> logger,
            IMemoryCache cache,
            IAsyncPolicy<HttpResponseMessage> retryPolicy,
            IAsyncPolicy<HttpResponseMessage> circuitBreakerPolicy)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _config = config.Value ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _retryPolicy = retryPolicy ?? throw new ArgumentNullException(nameof(retryPolicy));
            _circuitBreakerPolicy = circuitBreakerPolicy ?? throw new ArgumentNullException(nameof(circuitBreakerPolicy));

            // Combine retry and circuit breaker policies
            _combinedPolicy = Policy.WrapAsync(_retryPolicy, _circuitBreakerPolicy);

            ConfigureHttpClient();
        }

        public async Task<PropertyListing?> GetPropertyListingAsync(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                _logger.LogWarning("GetPropertyListingAsync called with null or empty address");
                return null;
            }

            var cacheKey = $"property_listing_{address.ToLowerInvariant()}";
            
            // Check cache first
            if (_config.EnableCaching && _cache.TryGetValue(cacheKey, out PropertyListing? cachedListing))
            {
                _logger.LogDebug("Retrieved property listing from cache for address: {Address}", address);
                return cachedListing;
            }

            _logger.LogInformation("Getting property listing for address: {Address}", address);

            try
            {
                var endpoint = $"/properties?address={Uri.EscapeDataString(address)}";
                
                var response = await _combinedPolicy.ExecuteAsync(async () =>
                {
                    var httpResponse = await _httpClient.GetAsync(endpoint);
                    
                    // Log the response for debugging
                    _logger.LogDebug("RentCast API response: {StatusCode} for endpoint: {Endpoint}", 
                        httpResponse.StatusCode, endpoint);
                    
                    return httpResponse;
                });

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("RentCast API returned {StatusCode} for address {Address}", 
                        response.StatusCode, address);
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<RentCastApiResponse>(content, GetJsonOptions());

                if (apiResponse?.Properties == null || !apiResponse.Properties.Any())
                {
                    _logger.LogInformation("No properties found for address: {Address}", address);
                    return null;
                }

                var property = apiResponse.Properties.First();
                var listing = ConvertToPropertyListing(property);

                // Cache the result
                if (_config.EnableCaching && listing != null)
                {
                    var cacheOptions = new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_config.CacheDurationMinutes),
                        Priority = CacheItemPriority.Normal
                    };
                    _cache.Set(cacheKey, listing, cacheOptions);
                    _logger.LogDebug("Cached property listing for address: {Address}", address);
                }

                return listing;
            }
            catch (BrokenCircuitException ex)
            {
                _logger.LogError(ex, "Circuit breaker is open for RentCast API. Address: {Address}", address);
                return null;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request failed for address {Address}", address);
                return null;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Request timeout for address {Address}", address);
                return null;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize response for address {Address}", address);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error getting property listing for address {Address}", address);
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
            if (string.IsNullOrWhiteSpace(city) || string.IsNullOrWhiteSpace(state))
            {
                _logger.LogWarning("GetRecentListingsAsync called with invalid parameters. City: {City}, State: {State}", city, state);
                return Array.Empty<PropertyListing>();
            }

            var cacheKey = $"recent_listings_{city.ToLowerInvariant()}_{state.ToLowerInvariant()}_{daysBack}";
            
            // Check cache first
            if (_config.EnableCaching && _cache.TryGetValue(cacheKey, out PropertyListing[]? cachedListings))
            {
                _logger.LogDebug("Retrieved recent listings from cache for {City}, {State}", city, state);
                return cachedListings ?? Array.Empty<PropertyListing>();
            }

            _logger.LogInformation("Getting recent listings for {City}, {State} (last {Days} days)", city, state, daysBack);

            try
            {
                var endpoint = $"/listings/sale?city={Uri.EscapeDataString(city)}&state={Uri.EscapeDataString(state)}&daysBack={daysBack}";
                
                var response = await _combinedPolicy.ExecuteAsync(async () =>
                    await _httpClient.GetAsync(endpoint));

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("RentCast API returned {StatusCode} for recent listings", response.StatusCode);
                    return Array.Empty<PropertyListing>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var listingsResponse = JsonSerializer.Deserialize<RentCastListingsResponse>(content, GetJsonOptions());

                var listings = listingsResponse?.Listings?.Select(ConvertFromRentCastListing).ToArray() 
                    ?? Array.Empty<PropertyListing>();

                // Cache the result
                if (_config.EnableCaching)
                {
                    var cacheOptions = new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_config.CacheDurationMinutes / 2), // Shorter cache for listings
                        Priority = CacheItemPriority.Normal
                    };
                    _cache.Set(cacheKey, listings, cacheOptions);
                }

                return listings;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent listings for {City}, {State}", city, state);
                return Array.Empty<PropertyListing>();
            }
        }

        public async Task<PropertyListing[]> GetStateListingsAsync(string state)
        {
            if (string.IsNullOrWhiteSpace(state))
            {
                _logger.LogWarning("GetStateListingsAsync called with null or empty state");
                return Array.Empty<PropertyListing>();
            }

            var cacheKey = $"state_listings_{state.ToLowerInvariant()}";
            
            // Check cache first
            if (_config.EnableCaching && _cache.TryGetValue(cacheKey, out PropertyListing[]? cachedListings))
            {
                _logger.LogDebug("Retrieved state listings from cache for state: {State}", state);
                return cachedListings ?? Array.Empty<PropertyListing>();
            }

            _logger.LogInformation("Getting all listings for state: {State}", state);

            try
            {
                var endpoint = $"/listings/sale?state={Uri.EscapeDataString(state)}";
                
                var response = await _combinedPolicy.ExecuteAsync(async () =>
                    await _httpClient.GetAsync(endpoint));

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("RentCast API returned {StatusCode} for state listings", response.StatusCode);
                    return Array.Empty<PropertyListing>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var listingsResponse = JsonSerializer.Deserialize<RentCastListingsResponse>(content, GetJsonOptions());

                var listings = listingsResponse?.Listings?.Select(ConvertFromRentCastListing).ToArray() 
                    ?? Array.Empty<PropertyListing>();

                // Cache the result with longer expiration for state-wide data
                if (_config.EnableCaching)
                {
                    var cacheOptions = new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_config.CacheDurationMinutes * 2),
                        Priority = CacheItemPriority.Low // Lower priority for large datasets
                    };
                    _cache.Set(cacheKey, listings, cacheOptions);
                }

                return listings;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting state listings for {State}", state);
                return Array.Empty<PropertyListing>();
            }
        }

        public async Task<PropertyListing[]> GetNewListingsAsync(string state, DateTime sinceDate)
        {
            if (string.IsNullOrWhiteSpace(state))
            {
                _logger.LogWarning("GetNewListingsAsync called with null or empty state");
                return Array.Empty<PropertyListing>();
            }

            _logger.LogInformation("Getting new listings for state {State} since {Since}", state, sinceDate);

            try
            {
                var sinceParam = sinceDate.ToString("yyyy-MM-dd");
                var endpoint = $"/listings/sale?state={Uri.EscapeDataString(state)}&listedSince={sinceParam}";
                
                var response = await _combinedPolicy.ExecuteAsync(async () =>
                    await _httpClient.GetAsync(endpoint));

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("RentCast API returned {StatusCode} for new listings", response.StatusCode);
                    return Array.Empty<PropertyListing>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var listingsResponse = JsonSerializer.Deserialize<RentCastListingsResponse>(content, GetJsonOptions());

                var listings = listingsResponse?.Listings?.Select(ConvertFromRentCastListing).ToArray() 
                    ?? Array.Empty<PropertyListing>();

                return listings;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting new listings for {State} since {Since}", state, sinceDate);
                return Array.Empty<PropertyListing>();
            }
        }

        private void ConfigureHttpClient()
        {
            _httpClient.BaseAddress = new Uri(_config.BaseUrl);
            _httpClient.Timeout = TimeSpan.FromSeconds(_config.TimeoutSeconds);
            
            // Add API key header
            _httpClient.DefaultRequestHeaders.Add("X-API-Key", _config.ApiKey);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "MemberPropertyAlert/1.0");
            
            _logger.LogDebug("Configured HTTP client for RentCast API. BaseUrl: {BaseUrl}, Timeout: {Timeout}s", 
                _config.BaseUrl, _config.TimeoutSeconds);
        }

        private static JsonSerializerOptions GetJsonOptions()
        {
            return new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
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

        private PropertyListing ConvertFromRentCastListing(RentCastListing listing)
        {
            return new PropertyListing
            {
                Id = listing.Id ?? Guid.NewGuid().ToString(),
                Address = listing.Address ?? string.Empty,
                City = listing.City ?? string.Empty,
                State = listing.State ?? string.Empty,
                ZipCode = listing.ZipCode ?? string.Empty,
                Price = listing.Price,
                Bedrooms = listing.Bedrooms,
                Bathrooms = listing.Bathrooms,
                SquareFootage = listing.SquareFootage,
                ListingDate = listing.ListingDate,
                DaysOnMarket = listing.DaysOnMarket,
                PropertyType = listing.PropertyType ?? string.Empty,
                MlsNumber = listing.MlsNumber ?? string.Empty,
                ListingAgent = listing.ListingAgent ?? string.Empty,
                Description = listing.Description ?? string.Empty,
                Photos = listing.Photos ?? new List<string>(),
                Status = listing.Status ?? "unknown",
                LastUpdated = listing.LastUpdated ?? DateTime.UtcNow
            };
        }

        private static string ExtractAddress(string? formattedAddress)
        {
            if (string.IsNullOrEmpty(formattedAddress)) return string.Empty;
            var parts = formattedAddress.Split(',');
            return parts.Length > 0 ? parts[0].Trim() : string.Empty;
        }

        private static string ExtractCity(string? formattedAddress)
        {
            if (string.IsNullOrEmpty(formattedAddress)) return string.Empty;
            var parts = formattedAddress.Split(',');
            return parts.Length > 1 ? parts[1].Trim() : string.Empty;
        }

        private static string ExtractState(string? formattedAddress)
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

        private static string ExtractZipCode(string? formattedAddress)
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

    // Response models for MockRentCastAPI compatibility
    public class RentCastListingsResponse
    {
        public List<RentCastListing>? Listings { get; set; }
        public int Count { get; set; }
        public string? Status { get; set; }
        public PaginationInfo? Pagination { get; set; }
    }

    public class RentCastListing
    {
        public string? Id { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? ZipCode { get; set; }
        public decimal? Price { get; set; }
        public int? Bedrooms { get; set; }
        public int? Bathrooms { get; set; }
        public int? SquareFootage { get; set; }
        public DateTime? ListingDate { get; set; }
        public int? DaysOnMarket { get; set; }
        public string? PropertyType { get; set; }
        public string? MlsNumber { get; set; }
        public string? ListingAgent { get; set; }
        public string? ListingOffice { get; set; }
        public string? Description { get; set; }
        public List<string>? Photos { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? Status { get; set; }
        public DateTime? LastUpdated { get; set; }
    }

    public class PaginationInfo
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public bool HasNext { get; set; }
        public bool HasPrevious { get; set; }
    }
}
