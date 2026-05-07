using System.ComponentModel.DataAnnotations;
using SaigonRide.Domain.Enums;

namespace SaigonRide.Domain.Entities;

/// <summary>
/// History row written whenever a vehicle's <see cref="Vehicle.Status"/>
/// transitions to/from <see cref="VehicleStatus.Maintenance"/> (UC-01 S-3,
/// UC-02 E-7). ERD table 9.
/// </summary>
public class MaintenanceLog
{
    public int Id { get; set; }

    public int VehicleId { get; set; }

    public Vehicle? Vehicle { get; set; }

    /// <summary>Admin/user who triggered the change.</summary>
    public int? UserId { get; set; }

    public User? User { get; set; }

    public VehicleStatus FromStatus { get; set; }

    public VehicleStatus ToStatus { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime LoggedAt { get; set; } = DateTime.UtcNow;
}
