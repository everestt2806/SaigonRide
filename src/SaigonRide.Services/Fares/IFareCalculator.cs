using SaigonRide.Domain.Entities;
using SaigonRide.Domain.ValueObjects;

namespace SaigonRide.Services.Fares;

/// <summary>
/// Pure-function fare engine (D-06). Owns the 15% discount rule (FR-08, BR-03)
/// and the round-up-to-the-minute policy (BR-02).
/// </summary>
public interface IFareCalculator
{
    FareBreakdown Calculate(DateTime startTime, DateTime endTime, decimal ratePerMinVnd, Station returnStation);
}
