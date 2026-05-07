using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SaigonRide.Domain.Enums;

namespace SaigonRide.Domain.Entities;

/// <summary>
/// Transactional fact table for trips (ERD table 7, CRC card 8). Holds two
/// station FKs (D-08): pickup is non-null at start, return is set at end.
/// <see cref="RatePerMinSnapshot"/> enforces BR-05 (rate is captured at start).
/// </summary>
public class Rental
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public User? User { get; set; }

    public int VehicleId { get; set; }

    public Vehicle? Vehicle { get; set; }

    public int PickupStationId { get; set; }

    [ForeignKey(nameof(PickupStationId))]
    public Station? PickupStation { get; set; }

    public int? ReturnStationId { get; set; }

    [ForeignKey(nameof(ReturnStationId))]
    public Station? ReturnStation { get; set; }

    public DateTime StartTime { get; set; } = DateTime.UtcNow;

    public DateTime? EndTime { get; set; }

    public RentalStatus Status { get; set; } = RentalStatus.Active;

    /// <summary>Rate per minute captured at start. BR-05 (no retroactive rate change).</summary>
    public decimal RatePerMinSnapshot { get; set; }

    public int? DurationMinutes { get; set; }

    public decimal? BaseFare { get; set; }

    public decimal? Discount { get; set; }

    public decimal? TotalFare { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Timestamp]
    public byte[]? RowVersion { get; set; }

    public Transaction? Transaction { get; set; }
}
