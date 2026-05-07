using System.ComponentModel.DataAnnotations;
using SaigonRide.Domain.Enums;

namespace SaigonRide.Domain.Entities;

/// <summary>
/// Fleet inventory entity (ERD table 6, CRC card 5). License plate is unique;
/// <see cref="RowVersion"/> is the EF concurrency token used by both UC-01 E-5
/// and UC-02 E-1 to detect concurrent edits.
/// </summary>
public class Vehicle
{
    public int Id { get; set; }

    [Required, MaxLength(20)]
    public string LicensePlate { get; set; } = string.Empty;

    public int VehicleCategoryId { get; set; }

    public VehicleCategory? Category { get; set; }

    public int HomeStationId { get; set; }

    public Station? HomeStation { get; set; }

    public VehicleStatus Status { get; set; } = VehicleStatus.Available;

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    [MaxLength(255)]
    public string? DecommissionReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Concurrency token (NFR-06). EF Core marks this as <c>rowversion</c>.</summary>
    [Timestamp]
    public byte[]? RowVersion { get; set; }

    public ICollection<Rental> Rentals { get; set; } = new List<Rental>();

    public ICollection<MaintenanceLog> MaintenanceLogs { get; set; } = new List<MaintenanceLog>();

    /// <summary>Encapsulates the status transition + audit trigger (Tell-Don't-Ask).</summary>
    public void ChangeStatus(VehicleStatus newStatus)
    {
        if (Status == VehicleStatus.Decommissioned && newStatus != VehicleStatus.Decommissioned)
            throw new InvalidOperationException("Decommissioned vehicles cannot be reactivated.");
        Status = newStatus;
        UpdatedAt = DateTime.UtcNow;
    }
}
