using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SaigonRide.Domain.Enums;

namespace SaigonRide.Domain.Entities;

/// <summary>
/// Single physical table for all roles (LCT / Tourist / Admin) with a
/// <see cref="UserType"/> discriminator. Maps to ERD table 1 (Users).
/// Authentication is via bcrypt-hashed password (NFR-02, D-03).
/// </summary>
public class User
{
    public int Id { get; set; }

    [Required, MaxLength(120)]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    public string FullName { get; set; } = string.Empty;

    /// <summary>BCrypt hash, cost factor 12 (NFR-02).</summary>
    [Required, MaxLength(72)]
    public string PasswordHash { get; set; } = string.Empty;

    public UserType UserType { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; }

    /// <summary>Consecutive failed login attempts. Reset on successful login.</summary>
    public int FailedAttempts { get; set; }

    /// <summary>If set, login is blocked until this UTC time.</summary>
    public DateTime? LockoutEnd { get; set; }

    public DateTime? DeletedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public LocalCommuterDetails? LocalDetails { get; set; }

    public ForeignTouristDetails? TouristDetails { get; set; }

    public ICollection<Rental> Rentals { get; set; } = new List<Rental>();

    [NotMapped]
    public bool IsAdmin => UserType == UserType.Admin;
}
