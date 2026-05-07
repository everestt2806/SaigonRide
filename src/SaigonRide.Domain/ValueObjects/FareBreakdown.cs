namespace SaigonRide.Domain.ValueObjects;

/// <summary>
/// Immutable result of <c>FareCalculator.Calculate</c>. CRC card 12. Used by
/// the checkout view (UC-02 step 8) and the receipt (step 13).
/// </summary>
public record FareBreakdown(
    int DurationMinutes,
    decimal RatePerMinVnd,
    decimal BaseFare,
    decimal Discount,
    decimal TotalFare,
    bool DiscountApplied,
    decimal OccupancyPctAtReturn);
