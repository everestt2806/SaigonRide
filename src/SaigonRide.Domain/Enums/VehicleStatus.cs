namespace SaigonRide.Domain.Enums;

/// <summary>
/// Status transitions allowed for a vehicle: see CRC card 5 in §6.1.
/// Available ↔ InTransit ↔ Maintenance ↔ Decommissioned.
/// </summary>
public enum VehicleStatus
{
    Available = 1,
    InTransit = 2,
    Maintenance = 3,
    Decommissioned = 4
}
