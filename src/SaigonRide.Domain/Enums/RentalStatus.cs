namespace SaigonRide.Domain.Enums;

/// <summary>
/// Lifecycle states for a rental: §4.2 main scenario + alternate flows.
/// </summary>
public enum RentalStatus
{
    Active = 1,
    Completed = 2,
    CancelledFree = 3,
    CancelledPaid = 4,
    PaymentPending = 5
}
