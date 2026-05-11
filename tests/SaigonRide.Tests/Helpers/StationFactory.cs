using SaigonRide.Domain.Entities;

namespace SaigonRide.Tests.Helpers;

internal static class StationFactory
{
    /// <summary>
    /// Builds a station whose <c>OccupancyPct</c> exactly equals the supplied
    /// value. Default capacity is 10 000 so we can target two-decimal
    /// boundaries (e.g. 19.99%) without integer rounding loss.
    /// </summary>
    public static Station WithOccupancy(decimal occupancyPct, int capacity = 10_000)
    {
        var current = (int)Math.Round(capacity * (double)(occupancyPct / 100m), MidpointRounding.AwayFromZero);
        return new Station
        {
            Id = 1,
            Name = "Test Station",
            Address = "x",
            Capacity = capacity,
            CurrentCount = current,
            IsActive = true
        };
    }
}
