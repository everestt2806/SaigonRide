using SaigonRide.Domain.Entities;
using SaigonRide.Services.Fares;
using SaigonRide.Tests.Helpers;
using Xunit;

namespace SaigonRide.Tests.Fares;

/// <summary>
/// TC-FARE-EP: Equivalence-Partition tests on the duration and rate inputs of
/// <see cref="FareCalculator"/>. Refer to QA Report §3 (FR-08, BR-02).
/// </summary>
public class FareCalculatorEpTests
{
    private readonly FareCalculator _sut = new();

    public static IEnumerable<object[]> EpCases =>
        // duration (minutes), ratePerMin, expectedDuration, expectedBase
        new List<object[]>
        {
            // Class 1: very short trip rounded up to 1 minute (BR-02).
            new object[] { 0.0, 500m,  1, 500m },
            // Class 2: exactly 1 minute, low rate (Standard Bike).
            new object[] { 1.0, 500m,  1, 500m },
            // Class 3: medium trip, low rate.
            new object[] { 5.0, 500m,  5, 2500m },
            // Class 4: hour-long trip, mid rate (E-Bike).
            new object[] { 60.0, 1000m, 60, 60_000m },
            // Class 5: 4-hour tourist trip, high rate (E-Scooter).
            new object[] { 240.0, 1500m, 240, 360_000m },
            // Class 6: 24-hour upper bound, high rate.
            new object[] { 1440.0, 1500m, 1440, 2_160_000m },
        };

    [Theory]
    [MemberData(nameof(EpCases))]
    public void Calculate_returns_expected_base_fare_for_each_partition(double minutes, decimal rate, int expectedDuration, decimal expectedBase)
    {
        var start = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var end = start.AddMinutes(minutes);
        var station = StationFactory.WithOccupancy(50m);

        var fare = _sut.Calculate(start, end, rate, station);

        Assert.Equal(expectedDuration, fare.DurationMinutes);
        Assert.Equal(expectedBase, fare.BaseFare);
        Assert.False(fare.DiscountApplied, "50% occupancy should not trigger the rebalancing discount.");
        Assert.Equal(expectedBase, fare.TotalFare);
    }

    [Fact]
    public void Calculate_throws_for_negative_duration()
    {
        var start = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var end = start.AddMinutes(-5);
        var station = StationFactory.WithOccupancy(50m);

        Assert.Throws<ArgumentException>(() => _sut.Calculate(start, end, 500m, station));
    }

    [Fact]
    public void Calculate_throws_for_zero_rate()
    {
        var start = DateTime.UtcNow;
        Assert.Throws<ArgumentOutOfRangeException>(() => _sut.Calculate(start, start.AddMinutes(5), 0m, StationFactory.WithOccupancy(50m)));
    }
}
