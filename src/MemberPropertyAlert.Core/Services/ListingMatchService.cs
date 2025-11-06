using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MemberPropertyAlert.Core.Abstractions.Integrations;
using MemberPropertyAlert.Core.Abstractions.Messaging;
using MemberPropertyAlert.Core.Abstractions.Repositories;
using MemberPropertyAlert.Core.Abstractions.Services;
using MemberPropertyAlert.Core.Domain.Entities;
using MemberPropertyAlert.Core.Domain.Enums;
using MemberPropertyAlert.Core.Domain.ValueObjects;
using MemberPropertyAlert.Core.Models;
using MemberPropertyAlert.Core.Results;
using Microsoft.Extensions.Logging;

namespace MemberPropertyAlert.Core.Services;

public sealed class ListingMatchService : IListingMatchService
{
    private readonly IRentCastClient _rentCastClient;
    private readonly IMemberAddressRepository _memberAddressRepository;
    private readonly IListingMatchRepository _listingMatchRepository;
    private readonly IAlertPublisher _alertPublisher;
    private readonly ILogger<ListingMatchService> _logger;

    public ListingMatchService(
        IRentCastClient rentCastClient,
        IMemberAddressRepository memberAddressRepository,
        IListingMatchRepository listingMatchRepository,
        IAlertPublisher alertPublisher,
        ILogger<ListingMatchService> logger)
    {
        _rentCastClient = rentCastClient;
        _memberAddressRepository = memberAddressRepository;
        _listingMatchRepository = listingMatchRepository;
        _alertPublisher = alertPublisher;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyCollection<ListingMatch>>> FindMatchesAsync(
        string stateOrProvince,
        IReadOnlyCollection<TenantInstitutionScope> scopes,
        CancellationToken cancellationToken = default)
    {
        var listingsResult = await _rentCastClient.GetListingsAsync(stateOrProvince, cancellationToken);
        if (listingsResult.IsFailure)
        {
            return Result<IReadOnlyCollection<ListingMatch>>.Failure(listingsResult.Error ?? "Unable to fetch listings.");
        }

        var listings = listingsResult.Value ?? Array.Empty<RentCastListing>();
        var matches = new List<ListingMatch>();

        if (scopes.Count == 0)
        {
            _logger.LogInformation("No tenant scopes available for matching in {State}", stateOrProvince);
            return Result<IReadOnlyCollection<ListingMatch>>.Success(matches);
        }

        foreach (var scope in scopes)
        {
            var addresses = await _memberAddressRepository.ListByStateAsync(
                stateOrProvince,
                scope.TenantId,
                scope.InstitutionId,
                cancellationToken);

            if (addresses.Count == 0)
            {
                continue;
            }

            foreach (var listing in listings)
            {
                var matchedAddresses = addresses
                    .Where(a => a.IsActive && IsPotentialMatch(a, listing))
                    .Select(a => a.Id)
                    .ToArray();

                if (matchedAddresses.Length == 0)
                {
                    continue;
                }

                var severity = DetermineSeverity(listing);
                var match = ListingMatch.Create(
                    Guid.NewGuid().ToString("N"),
                    listing.ListingId,
                    listing.Address,
                    listing.MonthlyRent,
                    listing.ListingUrl,
                    severity,
                    matchedAddresses,
                    DateTimeOffset.UtcNow,
                    listing.Region);
                matches.Add(match);
            }
        }

        _logger.LogInformation("Matched {MatchCount} listings in {State}", matches.Count, stateOrProvince);
        return Result<IReadOnlyCollection<ListingMatch>>.Success(matches);
    }

    public async Task<Result> PublishMatchesAsync(IReadOnlyCollection<ListingMatch> matches, CancellationToken cancellationToken = default)
    {
        if (matches.Count == 0)
        {
            return Result.Success();
        }

        var addressesById = await LoadAddressesForMatchesAsync(matches, cancellationToken);
        var addressesToPersist = new Dictionary<string, Dictionary<string, MemberAddress>>(StringComparer.OrdinalIgnoreCase);

        foreach (var match in matches)
        {
            var matchedAddresses = match.MatchedAddressIds
                .Select(id => addressesById.TryGetValue(id, out var address) ? address : null)
                .Where(address => address is not null)
                .Cast<MemberAddress>()
                .ToList();

            if (matchedAddresses.Count == 0)
            {
                _logger.LogWarning("No address metadata found for match {MatchId}", match.Id);
            }
            else
            {
                match.SetTenancyDetails(
                    matchedAddresses.Select(address => address.TenantId),
                    matchedAddresses.Select(address => address.InstitutionId));

                foreach (var address in matchedAddresses)
                {
                    address.RecordMatch(match.ListingId, match.DetectedAtUtc);
                    if (!addressesToPersist.TryGetValue(address.InstitutionId, out var bucket))
                    {
                        bucket = new Dictionary<string, MemberAddress>(StringComparer.OrdinalIgnoreCase);
                        addressesToPersist[address.InstitutionId] = bucket;
                    }

                    bucket[address.Id] = address;
                }
            }

            var createResult = await _listingMatchRepository.CreateAsync(match, cancellationToken);
            if (createResult.IsFailure)
            {
                _logger.LogWarning("Failed to persist match {MatchId}: {Error}", match.Id, createResult.Error);
            }
        }

        foreach (var group in addressesToPersist)
        {
            var upsertResult = await _memberAddressRepository.UpsertBulkAsync(group.Key, group.Value.Values.ToList(), cancellationToken);
            if (upsertResult.IsFailure)
            {
                _logger.LogWarning("Failed to update match history for institution {InstitutionId}: {Error}", group.Key, upsertResult.Error);
            }
        }

        var publishResult = await _alertPublisher.PublishAsync(matches, cancellationToken);
        if (publishResult.IsFailure)
        {
            return publishResult;
        }

        return Result.Success();
    }

    private async Task<Dictionary<string, MemberAddress>> LoadAddressesForMatchesAsync(IReadOnlyCollection<ListingMatch> matches, CancellationToken cancellationToken)
    {
        var lookup = new Dictionary<string, MemberAddress>(StringComparer.OrdinalIgnoreCase);
        var states = matches
            .Select(match => match.ListingAddress.StateOrProvince)
            .Where(state => !string.IsNullOrWhiteSpace(state))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var state in states)
        {
            var addresses = await _memberAddressRepository.ListByStateAsync(state, cancellationToken: cancellationToken);
            foreach (var address in addresses)
            {
                lookup[address.Id] = address;
            }
        }

        return lookup;
    }

    public Task<PagedResult<ListingMatch>> GetRecentMatchesAsync(string? institutionId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        => _listingMatchRepository.ListRecentAsync(institutionId, pageNumber, pageSize, cancellationToken);

    private static bool IsPotentialMatch(MemberAddress address, RentCastListing listing)
    {
        if (!string.Equals(address.Address.StateOrProvince, listing.Address.StateOrProvince, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(address.Address.PostalCode, listing.Address.PostalCode, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (address.Address.Coordinate is not null && listing.Address.Coordinate is not null)
        {
            var distanceKm = address.Address.Coordinate.DistanceTo(listing.Address.Coordinate);
            return distanceKm <= 2.0;
        }

        return false;
    }

    private static AlertSeverity DetermineSeverity(RentCastListing listing)
    {
        if (listing.MonthlyRent >= 5000)
        {
            return AlertSeverity.Critical;
        }

        if (listing.MonthlyRent >= 2500)
        {
            return AlertSeverity.Warning;
        }

        return AlertSeverity.Informational;
    }
}
