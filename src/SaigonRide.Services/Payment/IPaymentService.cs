using SaigonRide.Domain.Enums;
using SaigonRide.Domain.ValueObjects;

namespace SaigonRide.Services.Payment;

public interface IPaymentService
{
    Task<PaymentResult> ProcessAsync(PaymentMethod method, decimal amount, string idempotencyKey, CancellationToken ct = default);
    bool IsAllowedFor(UserType userType, PaymentMethod method);
    IEnumerable<PaymentMethod> GetAllowedMethods(UserType userType);
}
