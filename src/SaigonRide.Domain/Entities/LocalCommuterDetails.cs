using System.ComponentModel.DataAnnotations;

namespace SaigonRide.Domain.Entities;

/// <summary>
/// 1-to-0..1 sub-table holding VN-specific identity fields. Decision §6.3.6:
/// keeps national-ID-only validation off the base Users table.
/// </summary>
public class LocalCommuterDetails
{
    public int UserId { get; set; }

    /// <summary>Vietnamese national ID, unique. FR-12.</summary>
    [Required, MaxLength(20)]
    public string NationalId { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? PhoneNumber { get; set; }

    public User? User { get; set; }
}
