using System;
using System.Text.RegularExpressions;

namespace MemberPropertyAlert.Core.Domain.ValueObjects;

public sealed record Address
{
    private static readonly Regex PostalCodePattern = new(@"^[0-9A-Za-z\- ]{3,15}$", RegexOptions.Compiled);

    public string Line1 { get; }
    public string? Line2 { get; }
    public string City { get; }
    public string StateOrProvince { get; }
    public string PostalCode { get; }
    public string CountryCode { get; }
    public GeoCoordinate? Coordinate { get; }

    private Address(
        string line1,
        string? line2,
        string city,
        string stateOrProvince,
        string postalCode,
        string countryCode,
        GeoCoordinate? coordinate)
    {
        Line1 = line1;
        Line2 = line2;
        City = city;
        StateOrProvince = stateOrProvince;
        PostalCode = postalCode;
        CountryCode = countryCode;
        Coordinate = coordinate;
    }

    public static Address Create(
        string line1,
        string? line2,
        string city,
        string stateOrProvince,
        string postalCode,
        string countryCode,
        GeoCoordinate? coordinate = null)
    {
        if (string.IsNullOrWhiteSpace(line1))
        {
            throw new ArgumentException("Line1 is required", nameof(line1));
        }

        if (string.IsNullOrWhiteSpace(city))
        {
            throw new ArgumentException("City is required", nameof(city));
        }

        if (string.IsNullOrWhiteSpace(stateOrProvince))
        {
            throw new ArgumentException("State or province is required", nameof(stateOrProvince));
        }

        if (string.IsNullOrWhiteSpace(countryCode) || countryCode.Length is < 2 or > 3)
        {
            throw new ArgumentException("Country code must be ISO alpha-2/3", nameof(countryCode));
        }

        if (!PostalCodePattern.IsMatch(postalCode))
        {
            throw new ArgumentException("Postal code format is invalid", nameof(postalCode));
        }

        return new Address(
            line1.Trim(),
            string.IsNullOrWhiteSpace(line2) ? null : line2.Trim(),
            city.Trim(),
            stateOrProvince.Trim(),
            postalCode.Trim(),
            countryCode.ToUpperInvariant(),
            coordinate);
    }

    public override string ToString()
        => string.Join(", ", new[] { Line1, Line2, City, StateOrProvince, PostalCode, CountryCode }.Where(v => !string.IsNullOrWhiteSpace(v)));
}
