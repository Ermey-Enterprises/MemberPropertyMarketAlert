using System;

namespace MemberPropertyAlert.Core.Domain.ValueObjects;

public sealed record GeoCoordinate
{
    public double Latitude { get; }
    public double Longitude { get; }

    private GeoCoordinate(double latitude, double longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }

    public static GeoCoordinate Create(double latitude, double longitude)
    {
        if (latitude is < -90 or > 90)
        {
            throw new ArgumentOutOfRangeException(nameof(latitude), latitude, "Latitude must be between -90 and 90.");
        }

        if (longitude is < -180 or > 180)
        {
            throw new ArgumentOutOfRangeException(nameof(longitude), longitude, "Longitude must be between -180 and 180.");
        }

        return new GeoCoordinate(Math.Round(latitude, 6), Math.Round(longitude, 6));
    }

    public double DistanceTo(GeoCoordinate other)
    {
        const double EarthRadiusKm = 6371d;
        var lat = DegreesToRadians(other.Latitude - Latitude);
        var lon = DegreesToRadians(other.Longitude - Longitude);

        var a = Math.Pow(Math.Sin(lat / 2), 2) + Math.Cos(DegreesToRadians(Latitude)) * Math.Cos(DegreesToRadians(other.Latitude)) * Math.Pow(Math.Sin(lon / 2), 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return EarthRadiusKm * c;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180d;
}
