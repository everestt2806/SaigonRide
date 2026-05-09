using SaigonRide.Domain.Enums;
using SaigonRide.Domain.ValueObjects;

namespace SaigonRide.Services.Payment;

/// <summary>
/// Strategy abstraction for the five concrete payment gateways (CRC card 10
/// + 11). Tier 3 returns a synthetic <see cref="PaymentResult"/>; Tier 4 swaps
/// the same interface for live SDK calls without changing callers (D-04).
/// </summary>
public interface IPaymentGateway
{
    PaymentMethod Method { get; }

    /// <summary>Authorise and capture a payment.</summary>
    Task<PaymentResult> ProcessAsync(decimal amount, string idempotencyKey, CancellationToken ct = default);

    /// <summary>Refund a previously captured transaction identified by its gateway reference.</summary>
    Task<PaymentResult> RefundAsync(string gatewayRef, decimal amount, CancellationToken ct = default);
}
