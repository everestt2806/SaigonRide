using System.ComponentModel.DataAnnotations;

namespace SaigonRide.Domain.Entities;

/// <summary>
/// Master data for vehicle types: Standard Bike (500₫/min), E-Bike, E-Scooter
/// (1500₫/min). Maps to ERD table 4. Rate change is not retroactive (BR-05).
/// </summary>
public class VehicleCategory
{
    public int Id { get; set; }

    [Required, MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Rate per minute in VND. FR-08 fare formula input.</summary>
    public decimal RatePerMinVnd { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
}
