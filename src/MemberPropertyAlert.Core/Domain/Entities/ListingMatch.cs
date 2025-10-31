using System;
using System.Collections.Generic;
using System.Linq;
using MemberPropertyAlert.Core.Domain.Enums;
using MemberPropertyAlert.Core.Domain.ValueObjects;

namespace MemberPropertyAlert.Core.Domain.Entities;

public sealed class ListingMatch : Entity
{
    private readonly List<string> _matchedAddressIds = new();

    public string ListingId { get; }
    public Address ListingAddress { get; }
    public decimal MonthlyRent { get; }
    public Uri ListingUrl { get; }
    public AlertSeverity Severity { get; }
    public IReadOnlyCollection<string> MatchedAddressIds => _matchedAddressIds.AsReadOnly();
    public DateTimeOffset DetectedAtUtc { get; }
    public string? RentCastRegion { get; }
    public IDictionary<string, object> Metadata { get; }

    private ListingMatch(
        string id,
        string listingId,
        Address listingAddress,
        decimal monthlyRent,
        Uri listingUrl,
        AlertSeverity severity,
        IEnumerable<string> matchedAddressIds,
        DateTimeOffset detectedAtUtc,
        string? rentCastRegion,
        IDictionary<string, object>? metadata,
        DateTimeOffset? createdAtUtc = null,
        DateTimeOffset? updatedAtUtc = null)
        : base(id, createdAtUtc, updatedAtUtc)
    {
        ListingId = listingId;
        ListingAddress = listingAddress;
        MonthlyRent = monthlyRent;
        ListingUrl = listingUrl;
        Severity = severity;
        _matchedAddressIds.AddRange(matchedAddressIds);
        DetectedAtUtc = detectedAtUtc;
        RentCastRegion = rentCastRegion;
        Metadata = metadata ?? new Dictionary<string, object>();
    }

    public static ListingMatch Create(
        string id,
        string listingId,
        Address address,
        decimal monthlyRent,
        Uri listingUrl,
        AlertSeverity severity,
        IEnumerable<string> matchedAddressIds,
        DateTimeOffset detectedAtUtc,
        string? rentCastRegion = null,
        IDictionary<string, object>? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(listingId))
        {
            throw new ArgumentException("Listing id is required", nameof(listingId));
        }

        var addresses = matchedAddressIds?.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToArray() ?? Array.Empty<string>();
        if (addresses.Length == 0)
        {
            throw new ArgumentException("At least one matched address id must be provided", nameof(matchedAddressIds));
        }

        if (monthlyRent <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(monthlyRent), monthlyRent, "Monthly rent must be greater than zero.");
        }

        return new ListingMatch(id, listingId.Trim(), address, decimal.Round(monthlyRent, 2), listingUrl, severity, addresses, detectedAtUtc, rentCastRegion, metadata);
    }

    public static ListingMatch Rehydrate(
        string id,
        string listingId,
        Address address,
        decimal monthlyRent,
        Uri listingUrl,
        AlertSeverity severity,
        IEnumerable<string> matchedAddressIds,
        DateTimeOffset detectedAtUtc,
        string? rentCastRegion,
        IDictionary<string, object>? metadata,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
        => new(id, listingId, address, monthlyRent, listingUrl, severity, matchedAddressIds, detectedAtUtc, rentCastRegion, metadata, createdAtUtc, updatedAtUtc);
}
