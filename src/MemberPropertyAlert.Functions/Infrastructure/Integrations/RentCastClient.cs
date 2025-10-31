using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using MemberPropertyAlert.Core.Abstractions.Integrations;
using MemberPropertyAlert.Core.Domain.ValueObjects;
using MemberPropertyAlert.Core.Models;
using MemberPropertyAlert.Core.Options;
using MemberPropertyAlert.Core.Results;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MemberPropertyAlert.Functions.Infrastructure.Integrations;

public sealed class RentCastClient : IRentCastClient
{
    private readonly HttpClient _httpClient;
    private readonly RentCastOptions _options;
    private readonly ILogger<RentCastClient> _logger;

    public RentCastClient(HttpClient httpClient, IOptions<RentCastOptions> options, ILogger<RentCastClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        if (!string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        }

        if (_options.Timeout > TimeSpan.Zero)
        {
            _httpClient.Timeout = _options.Timeout;
        }

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Remove("X-Api-Key");
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Api-Key", _options.ApiKey);
        }
    }

    public async Task<Result<IReadOnlyCollection<RentCastListing>>> GetListingsAsync(string stateOrProvince, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/v1/listings?state={stateOrProvince}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("RentCast API responded with {Status}: {Body}", response.StatusCode, body);
                return Result<IReadOnlyCollection<RentCastListing>>.Failure($"RentCast API returned {(int)response.StatusCode}.");
            }

            var payload = await response.Content.ReadFromJsonAsync<List<RentCastListingDto>>(cancellationToken: cancellationToken) ?? new List<RentCastListingDto>();
            var listings = payload.Select(dto => dto.ToListing()).ToList();
            return Result<IReadOnlyCollection<RentCastListing>>.Success(listings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch listings for state {State}", stateOrProvince);
            return Result<IReadOnlyCollection<RentCastListing>>.Failure(ex.Message);
        }
    }

    public async Task<Result<RentCastListing?>> GetListingAsync(string listingId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/v1/listings/{listingId}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return Result<RentCastListing?>.Failure($"RentCast API returned {(int)response.StatusCode}.");
            }

            var dto = await response.Content.ReadFromJsonAsync<RentCastListingDto>(cancellationToken: cancellationToken);
            return Result<RentCastListing?>.Success(dto?.ToListing());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch listing {ListingId}", listingId);
            return Result<RentCastListing?>.Failure(ex.Message);
        }
    }

    private sealed record RentCastListingDto(
        string Id,
        string AddressLine1,
        string? AddressLine2,
        string City,
        string State,
        string PostalCode,
        string Country,
        decimal MonthlyRent,
        decimal? PricePerSqft,
        double? Bedrooms,
        double? Bathrooms,
        int? SquareFeet,
        string Url,
        DateTimeOffset ListedOn,
        double? Latitude,
        double? Longitude,
        string? Region)
    {
        public RentCastListing ToListing()
        {
            var coordinate = Latitude.HasValue && Longitude.HasValue ? GeoCoordinate.Create(Latitude.Value, Longitude.Value) : null;
            var address = Address.Create(AddressLine1, AddressLine2, City, State, PostalCode, Country, coordinate);
            return new RentCastListing(Id, address, MonthlyRent, PricePerSqft, Bedrooms, Bathrooms, SquareFeet, new Uri(Url), ListedOn, Region);
        }
    }
}
