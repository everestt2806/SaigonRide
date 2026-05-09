using SaigonRide.Domain.Enums;
using SaigonRide.Domain.ValueObjects;

namespace SaigonRide.Services.Payment.Gateways;

/// <summary>Cash settlement at the station — always succeeds synchronously.</summary>
public class CashGateway : IPaymentGateway
{
    public PaymentMethod Method => PaymentMethod.Cash;

    public Task<PaymentResult> ProcessAsync(decimal amount, string idempotencyKey, CancellationToken ct = default)
        {
            return Task.FromResult(new PaymentResult(Success: true, GatewayRef: "CASH"));
        }

        public Task<PaymentResult> RefundAsync(string gatewayRef, decimal amount, CancellationToken ct = default)
        {
            return Task.FromResult(new PaymentResult(Success: true, GatewayRef: "CASH_REFUND"));
        }
    }
