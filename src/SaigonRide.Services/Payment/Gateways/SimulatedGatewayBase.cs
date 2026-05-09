using SaigonRide.Domain.Enums;
using SaigonRide.Domain.ValueObjects;

namespace SaigonRide.Services.Payment.Gateways;

/// <summary>
/// Tier-3 simulator base: returns a synthetic success with a deterministic
/// gateway reference. Tier-4 swaps each subclass for a live SDK call.
/// </summary>
public abstract class SimulatedGatewayBase : IPaymentGateway
{
    public abstract PaymentMethod Method { get; }
    protected abstract string Prefix { get; }

    public virtual Task<PaymentResult> ProcessAsync(decimal amount, string idempotencyKey, CancellationToken ct = default)
        {
            var gatewayRef = $"{Prefix}_SIM_{idempotencyKey}_{DateTime.UtcNow:yyyyMMddHHmmss}";
            return Task.FromResult(new PaymentResult(Success: true, GatewayRef: gatewayRef));
        }

        public virtual Task<PaymentResult> RefundAsync(string gatewayRef, decimal amount, CancellationToken ct = default)
        {
            var refundRef = $"{Prefix}_REFUND_{gatewayRef}_{DateTime.UtcNow:yyyyMMddHHmmss}";
            return Task.FromResult(new PaymentResult(Success: true, GatewayRef: refundRef));
        }
    }
