using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MemberPropertyAlert.Core.Abstractions.Integrations;
using MemberPropertyAlert.Core.Abstractions.Messaging;
using MemberPropertyAlert.Core.Abstractions.Repositories;
using MemberPropertyAlert.Core.Domain.Entities;
using MemberPropertyAlert.Core.Domain.Enums;
using MemberPropertyAlert.Core.Domain.ValueObjects;
using MemberPropertyAlert.Core.Models;
using MemberPropertyAlert.Core.Results;
using MemberPropertyAlert.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MemberPropertyAlert.Core.Tests;

public class ListingMatchServiceTests
{
    [Fact]
    public async Task PublishMatchesAsync_StampsTenancyAndUpdatesMemberAddresses()
    {
        var address = CreateMemberAddress(
            "addr-1",
            "tenant-1",
            "inst-1",
            Address.Create("123 Main St", null, "Metropolis", "CA", "90210", "US"));

        var memberRepository = new FakeMemberAddressRepository(new[] { address });
        var matchRepository = new FakeListingMatchRepository();
        var alertPublisher = new FakeAlertPublisher();
        var service = new ListingMatchService(
            new NoOpRentCastClient(),
            memberRepository,
            matchRepository,
            alertPublisher,
            NullLogger<ListingMatchService>.Instance);

        var match = ListingMatch.Create(
            Guid.NewGuid().ToString("N"),
            "listing-1",
            Address.Create("456 Elm St", null, "Metropolis", "CA", "90210", "US"),
            2500m,
            new Uri("https://example.com/listing-1"),
            AlertSeverity.Warning,
            new[] { address.Id },
            DateTimeOffset.UtcNow,
            "CA");

        var result = await service.PublishMatchesAsync(new[] { match });

        Assert.True(result.IsSuccess);
        Assert.Single(match.MatchedTenantIds);
        Assert.Equal(address.TenantId, match.MatchedTenantIds.Single());
        Assert.Single(match.MatchedInstitutionIds);
        Assert.Equal(address.InstitutionId, match.MatchedInstitutionIds.Single());

        var persistedMatch = Assert.Single(matchRepository.CreatedMatches);
        Assert.Equal(match.Id, persistedMatch.Id);

        var updatedAddresses = memberRepository.GetUpdatesForInstitution(address.InstitutionId);
        var updatedAddress = Assert.Single(updatedAddresses);
        Assert.Equal(match.ListingId, updatedAddress.LastMatchedListingId);
        Assert.Equal(match.DetectedAtUtc, updatedAddress.LastMatchedAtUtc);

        var publishedBatch = Assert.Single(alertPublisher.PublishedBatches);
        Assert.Contains(match, publishedBatch);
    }

    private static MemberAddress CreateMemberAddress(string id, string tenantId, string institutionId, Address address)
    {
        var result = MemberAddress.Create(id, tenantId, institutionId, address);
        Assert.True(result.IsSuccess, result.Error);
        return result.Value!;
    }

    private sealed class NoOpRentCastClient : IRentCastClient
    {
        public Task<Result<IReadOnlyCollection<RentCastListing>>> GetListingsAsync(string stateOrProvince, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<IReadOnlyCollection<RentCastListing>>.Success(Array.Empty<RentCastListing>()));

        public Task<Result<RentCastListing?>> GetListingAsync(string listingId, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<RentCastListing?>.Success(null));
    }

    private sealed class FakeMemberAddressRepository : IMemberAddressRepository
    {
        private readonly Dictionary<string, List<MemberAddress>> _addressesByState = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<MemberAddress>> _updates = new(StringComparer.OrdinalIgnoreCase);

        public FakeMemberAddressRepository(IEnumerable<MemberAddress> addresses)
        {
            foreach (var address in addresses)
            {
                var state = address.Address.StateOrProvince;
                if (!_addressesByState.TryGetValue(state, out var list))
                {
                    list = new List<MemberAddress>();
                    _addressesByState[state] = list;
                }

                list.Add(address);
            }
        }

        public Task<Result<MemberAddress>> CreateAsync(MemberAddress address, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Result<MemberAddress?>> GetAsync(string institutionId, string addressId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<PagedResult<MemberAddress>> ListByInstitutionAsync(string institutionId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyCollection<MemberAddress>> ListByStateAsync(string stateOrProvince, CancellationToken cancellationToken = default)
        {
            if (_addressesByState.TryGetValue(stateOrProvince, out var list))
            {
                return Task.FromResult<IReadOnlyCollection<MemberAddress>>(list);
            }

            return Task.FromResult<IReadOnlyCollection<MemberAddress>>(Array.Empty<MemberAddress>());
        }

        public Task<Result> UpsertBulkAsync(string institutionId, IReadOnlyCollection<MemberAddress> addresses, CancellationToken cancellationToken = default)
        {
            _updates[institutionId] = addresses.ToList();
            return Task.FromResult(Result.Success());
        }

        public Task<Result> DeleteAsync(string institutionId, string addressId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public IReadOnlyCollection<MemberAddress> GetUpdatesForInstitution(string institutionId)
            => _updates.TryGetValue(institutionId, out var list) ? list : Array.Empty<MemberAddress>();
    }

    private sealed class FakeListingMatchRepository : IListingMatchRepository
    {
        public List<ListingMatch> CreatedMatches { get; } = new();

        public Task<Result<ListingMatch>> CreateAsync(ListingMatch match, CancellationToken cancellationToken = default)
        {
            CreatedMatches.Add(match);
            return Task.FromResult(Result<ListingMatch>.Success(match));
        }

        public Task<PagedResult<ListingMatch>> ListRecentAsync(string? institutionId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Result> PurgeOlderThanAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class FakeAlertPublisher : IAlertPublisher
    {
        public List<IReadOnlyCollection<ListingMatch>> PublishedBatches { get; } = new();

        public Task<Result> PublishAsync(IReadOnlyCollection<ListingMatch> matches, CancellationToken cancellationToken = default)
        {
            PublishedBatches.Add(matches);
            return Task.FromResult(Result.Success());
        }
    }
}
