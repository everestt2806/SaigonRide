using System.ComponentModel.DataAnnotations;
using SaigonRide.Domain.Enums;

namespace SaigonRide.Domain.Entities;

/// <summary>
/// Links a user to a registered payment method. Enables the system to
/// know which payment methods a user can actually use (unlike the Phase 2
/// design which only filtered by UserType).
/// </summary>
public class UserPaymentMethod
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public User? User { get; set; }

    public PaymentMethod Method { get; set; }

    /// <summary>Masked account identifier for display, e.g. "MoMo: 0903***123".</summary>
    [MaxLength(120)]
    public string? MaskedAccount { get; set; }

    public bool IsVerified { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}