using SaigonRide.Domain.Entities;
using SaigonRide.Domain.ValueObjects;

namespace SaigonRide.Services.Fares;

/// <summary>
/// Pure-function fare engine. Implements FR-08 with the 15% rebalancing
/// discount when the return station is below 20% occupancy and BR-02 minimum
/// 1-minute duration. Threshold and discount rate are configurable.
/// </summary>
public class FareCalculator : IFareCalculator
{
    /// <summary>Default occupancy threshold for the discount (BR-03).</summary>
    public const decimal DefaultDiscountThresholdPct = 20m;

    /// <summary>Default discount rate (FR-08).</summary>
    public const decimal DefaultDiscountRate = 0.15m;

    /// <summary>Minimum fare floor (1 000 VND) so VNPay sandbox always gets a valid amount.</summary>
    public const decimal MinimumFare = 1000m;

    private readonly decimal _thresholdPct;
    private readonly decimal _discountRate;

    public FareCalculator() : this(DefaultDiscountThresholdPct, DefaultDiscountRate) { }

    public FareCalculator(decimal thresholdPct, decimal discountRate)
    {
        if (thresholdPct < 0 || thresholdPct > 100)
            throw new ArgumentOutOfRangeException(nameof(thresholdPct));
        if (discountRate < 0 || discountRate > 1)
            throw new ArgumentOutOfRangeException(nameof(discountRate));
        _thresholdPct = thresholdPct;
        _discountRate = discountRate;
    }

    public FareBreakdown Calculate(DateTime startTime, DateTime endTime, decimal ratePerMinVnd, Station returnStation)
    {
        if (returnStation is null) throw new ArgumentNullException(nameof(returnStation));
        if (ratePerMinVnd <= 0) throw new ArgumentOutOfRangeException(nameof(ratePerMinVnd));
        if (endTime < startTime)
            throw new ArgumentException("End time cannot be earlier than start time.", nameof(endTime));

        var elapsed = endTime - startTime;
        var duration = (int)Math.Max(1, Math.Ceiling(elapsed.TotalMinutes));
        var baseFare = decimal.Round(duration * ratePerMinVnd, 0, MidpointRounding.AwayFromZero);

        var occupancy = returnStation.OccupancyPct;
        var discountApplies = occupancy < _thresholdPct;
        var discount = discountApplies
            ? decimal.Round(baseFare * _discountRate, 0, MidpointRounding.AwayFromZero)
            : 0m;
        var total = Math.Max(baseFare - discount, MinimumFare);

        return new FareBreakdown(
            DurationMinutes: duration,
            RatePerMinVnd: ratePerMinVnd,
            BaseFare: baseFare,
            Discount: discount,
            TotalFare: total,
            DiscountApplied: discountApplies,
            OccupancyPctAtReturn: occupancy);
    }
}
