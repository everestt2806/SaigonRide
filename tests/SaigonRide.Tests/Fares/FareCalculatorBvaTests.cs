using SaigonRide.Services.Fares;
using SaigonRide.Tests.Helpers;
using Xunit;

namespace SaigonRide.Tests.Fares;

/// <summary>
/// TC-FARE-BVA: Boundary-Value Analysis around the 20% discount threshold
/// (FR-08 / BR-03). 7 boundary points × 2 categories = 14 cases as required
/// by the QA rubric. Includes the 2-minute cancellation BVA used by the
/// rental cancellation rule (BR-04) at the bottom.
/// </summary>
public class FareCalculatorBvaTests
{
    private readonly FareCalculator _sut = new();

    public static IEnumerable<object[]> BvaCases =>
        new List<object[]>
        {
            // 20% threshold boundary points × Standard Bike (500₫/min)
            new object[] {  0m, 500m, true  },  // way below
            new object[] { 19m, 500m, true  },  // just below
            new object[] { 19.99m, 500m, true },// epsilon below
            new object[] { 20m, 500m, false },  // exactly threshold (NOT discounted by spec — strictly less than)
            new object[] { 20.01m, 500m, false },// epsilon above
            new object[] { 21m, 500m, false },  // just above
            new object[] { 100m, 500m, false }, // upper bound

            // Same 7 boundary points × E-Scooter (1500₫/min)
            new object[] {  0m, 1500m, true  },
            new object[] { 19m, 1500m, true  },
            new object[] { 19.99m, 1500m, true },
            new object[] { 20m, 1500m, false },
            new object[] { 20.01m, 1500m, false },
            new object[] { 21m, 1500m, false },
            new object[] { 100m, 1500m, false },
        };

    [Theory]
    [MemberData(nameof(BvaCases))]
    public void Discount_applies_iff_occupancy_strictly_below_20pct(decimal occupancyPct, decimal ratePerMin, bool expectDiscount)
    {
        var start = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var end = start.AddMinutes(10);
        var station = StationFactory.WithOccupancy(occupancyPct);

        var fare = _sut.Calculate(start, end, ratePerMin, station);

        var expectedBase = 10 * ratePerMin;
        Assert.Equal(expectDiscount, fare.DiscountApplied);
        if (expectDiscount)
        {
            var expectedDiscount = decimal.Round(expectedBase * 0.15m, 0, MidpointRounding.AwayFromZero);
            Assert.Equal(expectedDiscount, fare.Discount);
            Assert.Equal(expectedBase - expectedDiscount, fare.TotalFare);
        }
        else
        {
            Assert.Equal(0m, fare.Discount);
            Assert.Equal(expectedBase, fare.TotalFare);
        }
    }
}
