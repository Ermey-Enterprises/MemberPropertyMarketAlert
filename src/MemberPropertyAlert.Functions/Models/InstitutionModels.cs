using System;
using System.Collections.Generic;
using MemberPropertyAlert.Core.Domain.Enums;
using MemberPropertyAlert.Core.Domain.ValueObjects;

namespace MemberPropertyAlert.Functions.Models;

public sealed record CreateInstitutionRequest(
    string TenantId,
    string Name,
    string TimeZoneId,
    string? PrimaryContactEmail);

public sealed record UpdateInstitutionRequest(
    string Name,
    InstitutionStatus Status,
    string? PrimaryContactEmail);

public sealed record MemberAddressRequest(
    string Line1,
    string? Line2,
    string City,
    string StateOrProvince,
    string PostalCode,
    string CountryCode,
    double? Latitude,
    double? Longitude,
    IReadOnlyCollection<string>? Tags);

public sealed record MemberAddressResponse(
    string Id,
    string InstitutionId,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? LastMatchedAtUtc,
    string? LastMatchedListingId,
    IReadOnlyCollection<string> Tags,
    AddressResponse Address);

public sealed record AddressResponse(
    string Line1,
    string? Line2,
    string City,
    string StateOrProvince,
    string PostalCode,
    string CountryCode,
    double? Latitude,
    double? Longitude)
{
    public static AddressResponse FromValueObject(Address address)
    {
        return new AddressResponse(
            address.Line1,
            address.Line2,
            address.City,
            address.StateOrProvince,
            address.PostalCode,
            address.CountryCode,
            address.Coordinate?.Latitude,
            address.Coordinate?.Longitude);
    }
}

public sealed record InstitutionResponse(
    string Id,
    string TenantId,
    string Name,
    InstitutionStatus Status,
    string TimeZoneId,
    string? PrimaryContactEmail,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    int AddressCount,
    IReadOnlyCollection<MemberAddressResponse>? Addresses);
