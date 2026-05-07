using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SaigonRide.Domain.Enums;

namespace SaigonRide.Domain.Entities;

/// <summary>
/// Physical pickup/drop-off location. Holds capacity and current count;
/// computes occupancy and the discount eligibility flag (FR-08, BR-03).
/// CRC card 7 in §6.1. ERD table 5. Increment/Decrement keep the invariant
/// 0 ≤ CurrentCount ≤ Capacity (Tell-Don't-Ask, §6.2.5).
/// </summary>
public class Station
{
    public int Id { get; set; }

    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(255)]
    public string Address { get; set; } = string.Empty;

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    /// <summary>Maximum vehicles that can be parked here. Must be > 0 (CHECK constraint).</summary>
    public int Capacity { get; set; }

    /// <summary>Current parked vehicle count. Must be ≥ 0 (CHECK constraint).</summary>
    public int CurrentCount { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Occupancy ratio used by the 15% discount rule (FR-08).</summary>
    [NotMapped]
    public decimal OccupancyPct => Capacity == 0 ? 0m : (decimal)CurrentCount / Capacity * 100m;

    [NotMapped]
    public StationStatus Status
    {
        get
        {
            if (CurrentCount >= Capacity) return StationStatus.Full;
            if (OccupancyPct < 20m) return StationStatus.LowInventory;
            return StationStatus.Normal;
        }
    }

    public ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();

    public bool HasCapacity() => CurrentCount < Capacity;

    /// <summary>True when CurrentCount exceeds Capacity due to forced returns (UC-02 E-3).</summary>
    [NotMapped]
    public bool IsOverflowed => CurrentCount > Capacity;

    public void Increment()
    {
        // UC-02 E-3: operational requirement — always accept returns.
        // Overflow is flagged with IsOverflowed, logged, and shown as a warning banner.
        CurrentCount++;
    }

    public void Decrement()
    {
        if (CurrentCount <= 0)
            throw new InvalidOperationException($"Station {Name} has no vehicles to remove.");
        CurrentCount--;
    }
}

