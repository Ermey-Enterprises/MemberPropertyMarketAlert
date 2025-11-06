using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MemberPropertyAlert.Core.Abstractions.Integrations;
using MemberPropertyAlert.Core.Abstractions.Messaging;
using MemberPropertyAlert.Core.Abstractions.Repositories;
using MemberPropertyAlert.Core.Domain.Entities;
using MemberPropertyAlert.Core.Domain.ValueObjects;
using MemberPropertyAlert.Core.Models;
using MemberPropertyAlert.Core.Results;
using MemberPropertyAlert.Core.Services;
using Microsoft.Extensions.Logging;

namespace MemberPropertyAlert.Core.Tests;

public sealed class ListingMatchServiceTests
{
    [Fact]
    public async Task FindMatchesAsync_ReturnsMatchesForAllScopes_WhenNoTenantContext()
    {
        // Arrange
        var listingAddress = Address.Create("123 Main St", null, "Los Angeles", "CA", "90001", "US");
        var listing = new RentCastListing(
            "listing-1",
            listingAddress,
            2500m,
            null,
            3,
            2,
            1200,
            new Uri("https://example.com/listing-1"),
            DateTimeOffset.UtcNow,
            "Los Angeles");

        var scopes = new[]
        {
            new TenantInstitutionScope("tenant-a", "inst-a"),
            new TenantInstitutionScope("tenant-b", "inst-b"),
        };

        var addresses = new Dictionary<(string TenantId, string InstitutionId), IReadOnlyCollection<MemberAddress>>
        {
            [("tenant-a", "inst-a")] = new[]
            {
                CreateMemberAddress("addr-a", "tenant-a", "inst-a", listingAddress)
            },
            [("tenant-b", "inst-b")] = new[]
            {
                CreateMemberAddress("addr-b", "tenant-b", "inst-b", listingAddress)
            }
        };

        var rentCastClient = new FakeRentCastClient(new[] { listing });
        var memberAddressRepository = new FakeMemberAddressRepository(addresses);
        var listingMatchRepository = new FakeListingMatchRepository();
        var alertPublisher = new FakeAlertPublisher();
        var logger = new NoOpLogger<ListingMatchService>();

        var service = new ListingMatchService(
            rentCastClient,
            memberAddressRepository,
            listingMatchRepository,
            alertPublisher,
            logger);

        // Act
        var result = await service.FindMatchesAsync("CA", scopes, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var matches = result.Value?.ToList();
        Assert.NotNull(matches);
        Assert.Equal(2, matches!.Count);
        Assert.Contains(matches, match => match.MatchedAddressIds.Contains("addr-a"));
        Assert.Contains(matches, match => match.MatchedAddressIds.Contains("addr-b"));

        Assert.Collection(memberAddressRepository.Requests,
            scope => Assert.Equal(("tenant-a", "inst-a"), scope),
            scope => Assert.Equal(("tenant-b", "inst-b"), scope));
    }

    private static MemberAddress CreateMemberAddress(string id, string tenantId, string institutionId, Address address)
    {
        var result = MemberAddress.Create(id, tenantId, institutionId, address);
        Assert.True(result.IsSuccess);
        return result.Value!;
    }

    private sealed class FakeRentCastClient : IRentCastClient
    {
        private readonly IReadOnlyCollection<RentCastListing> _listings;

        public FakeRentCastClient(IReadOnlyCollection<RentCastListing> listings)
        {
            _listings = listings;
        }

        public Task<Result<IReadOnlyCollection<RentCastListing>>> GetListingsAsync(string stateOrProvince, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result<IReadOnlyCollection<RentCastListing>>.Success(_listings));
        }

        public Task<Result<RentCastListing?>> GetListingAsync(string listingId, CancellationToken cancellationToken = default)
        {
            var listing = _listings.FirstOrDefault(l => l.ListingId == listingId);
            return Task.FromResult(Result<RentCastListing?>.Success(listing));
        }
    }

    private sealed class FakeMemberAddressRepository : IMemberAddressRepository
    {
        private readonly Dictionary<(string TenantId, string InstitutionId), IReadOnlyCollection<MemberAddress>> _addresses;

        public FakeMemberAddressRepository(Dictionary<(string TenantId, string InstitutionId), IReadOnlyCollection<MemberAddress>> addresses)
        {
            _addresses = addresses;
        }

        public List<(string TenantId, string InstitutionId)> Requests { get; } = new();

        public Task<Result<MemberAddress>> CreateAsync(MemberAddress address, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<Result<MemberAddress?>> GetAsync(string institutionId, string addressId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<PagedResult<MemberAddress>> ListByInstitutionAsync(string institutionId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyCollection<MemberAddress>> ListByStateAsync(
            string stateOrProvince,
            string? tenantId = null,
            string? institutionId = null,
            CancellationToken cancellationToken = default)
        {
            if (tenantId is null || institutionId is null)
            {
                return Task.FromResult<IReadOnlyCollection<MemberAddress>>(Array.Empty<MemberAddress>());
            }

            Requests.Add((tenantId, institutionId));

            if (_addresses.TryGetValue((tenantId, institutionId), out var scopedAddresses))
            {
                var filtered = scopedAddresses
                    .Where(address => string.Equals(address.Address.StateOrProvince, stateOrProvince, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                return Task.FromResult<IReadOnlyCollection<MemberAddress>>(filtered);
            }

            return Task.FromResult<IReadOnlyCollection<MemberAddress>>(Array.Empty<MemberAddress>());
        }

        public Task<Result> UpsertBulkAsync(string institutionId, IReadOnlyCollection<MemberAddress> addresses, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<Result> DeleteAsync(string institutionId, string addressId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class FakeListingMatchRepository : IListingMatchRepository
    {
        public Task<Result<ListingMatch>> CreateAsync(ListingMatch match, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result<ListingMatch>.Success(match));
        }

        public Task<PagedResult<ListingMatch>> ListRecentAsync(string? institutionId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<Result> PurgeOlderThanAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class FakeAlertPublisher : IAlertPublisher
    {
        public Task<Result> PublishAsync(IReadOnlyCollection<ListingMatch> matches, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result.Success());
        }
    }

    private sealed class NoOpLogger<T> : ILogger<T>
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => false;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
