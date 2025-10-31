using System;
using MemberPropertyAlert.Core.Domain.ValueObjects;

namespace MemberPropertyAlert.Core.Models;

public sealed record RentCastListing(
    string ListingId,
    Address Address,
    decimal MonthlyRent,
    decimal? PricePerSquareFoot,
    double? Bedrooms,
    double? Bathrooms,
    int? SquareFeet,
    Uri ListingUrl,
    DateTimeOffset ListedOnUtc,
    string? Region
);
