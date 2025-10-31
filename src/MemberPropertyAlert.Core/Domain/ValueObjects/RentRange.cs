using System;

namespace MemberPropertyAlert.Core.Domain.ValueObjects;

public sealed record RentRange
{
    public decimal Minimum { get; }
    public decimal Maximum { get; }

    private RentRange(decimal minimum, decimal maximum)
    {
        Minimum = minimum;
        Maximum = maximum;
    }

    public static RentRange Create(decimal minimum, decimal maximum)
    {
        if (minimum < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimum), minimum, "Minimum rent must be non-negative.");
        }

        if (maximum <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximum), maximum, "Maximum rent must be greater than zero.");
        }

        if (maximum < minimum)
        {
            throw new ArgumentException("Maximum rent must be greater than or equal to minimum rent.", nameof(maximum));
        }

        return new RentRange(decimal.Round(minimum, 2), decimal.Round(maximum, 2));
    }

    public bool Contains(decimal rent) => rent >= Minimum && rent <= Maximum;
}
