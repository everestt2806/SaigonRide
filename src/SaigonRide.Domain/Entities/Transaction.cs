using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SaigonRide.Domain.Enums;

namespace SaigonRide.Domain.Entities;

/// <summary>
/// Payment record, 1-to-0..1 with rental (ERD table 8, CRC card 9).
/// <c>RentalId</c> is unique to enforce one-transaction-per-rental.
/// Tracked through an explicit state machine (<see cref="TransactionStatus"/>)
/// instead of a simple IsPaid flag to distinguish Created / Processing /
/// Completed / Failed / Refunded.
/// </summary>
public class Transaction
{
    public int Id { get; set; }

    public int RentalId { get; set; }

    public Rental? Rental { get; set; }

    public PaymentMethod PaymentMethod { get; set; }

    public decimal Amount { get; set; }

    public decimal Discount { get; set; }

    /// <summary>State-machine status of the payment.</summary>
    public TransactionStatus Status { get; set; } = TransactionStatus.Created;

    [MaxLength(100)]
    public string? GatewayRef { get; set; }

    [MaxLength(64)]
    public string? IdempotencyKey { get; set; }

    public DateTime PaidAt { get; set; } = DateTime.UtcNow;

    /// <summary>Convenience: <c>true</c> when <see cref="Status"/> is <see cref="TransactionStatus.Completed"/>.</summary>
    [NotMapped]
    public bool IsPaid => Status == TransactionStatus.Completed;
}
