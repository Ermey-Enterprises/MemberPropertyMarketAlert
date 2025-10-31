using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using MemberPropertyAlert.Core.Domain.Enums;
using MemberPropertyAlert.Core.Domain.Entities;

namespace MemberPropertyAlert.Functions.Models;

public sealed record ListingMatchResponse(
    string Id,
    string ListingId,
    Uri ListingUrl,
    decimal MonthlyRent,
    AlertSeverity Severity,
    IReadOnlyCollection<string> MatchedAddressIds,
    DateTimeOffset DetectedAtUtc,
    string? RentCastRegion,
    AddressResponse ListingAddress,
    IReadOnlyDictionary<string, object> Metadata)
{
    public static ListingMatchResponse FromDomain(ListingMatch match)
    {
        return new ListingMatchResponse(
            match.Id,
            match.ListingId,
            match.ListingUrl,
            match.MonthlyRent,
            match.Severity,
            match.MatchedAddressIds,
            match.DetectedAtUtc,
            match.RentCastRegion,
            AddressResponse.FromValueObject(match.ListingAddress),
            new ReadOnlyDictionary<string, object>(match.Metadata));
    }
}
