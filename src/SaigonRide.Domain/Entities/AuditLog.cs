using System.ComponentModel.DataAnnotations;

namespace SaigonRide.Domain.Entities;

/// <summary>
/// Append-only mutation log used for compliance traceability (ERD table 10,
/// CRC card 14). One row per successful mutation (failed transactions do not
/// log anything — see UC-01 spec post-conditions).
/// </summary>
public class AuditLog
{
    public long Id { get; set; }

    public int? UserId { get; set; }

    public User? User { get; set; }

    [Required, MaxLength(80)]
    public string Action { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    public string EntityName { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? EntityId { get; set; }

    /// <summary>JSON diff or payload describing the mutation.</summary>
    public string? DetailJson { get; set; }

    public DateTime LoggedAt { get; set; } = DateTime.UtcNow;
}
