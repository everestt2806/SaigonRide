namespace SaigonRide.Domain.Enums;

/// <summary>
/// Computed station occupancy status used by RPT-02 and the discount rule (FR-08).
/// </summary>
public enum StationStatus
{
    LowInventory = 1,
    Normal = 2,
    Full = 3
}
