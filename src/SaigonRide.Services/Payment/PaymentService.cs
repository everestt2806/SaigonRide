using SaigonRide.Domain.Enums;
using SaigonRide.Domain.ValueObjects;

namespace SaigonRide.Services.Payment;

/// <summary>
/// Strategy dispatcher backed by a registry of <see cref="IPaymentGateway"/>
/// implementations (D-04, SOLID §7.2.1 OCP). Adding a sixth method only
/// requires implementing a new <see cref="IPaymentGateway"/> and registering
/// it in DI; this class is closed to modification.
/// </summary>
public class PaymentService : IPaymentService
{
    private readonly IReadOnlyDictionary<PaymentMethod, IPaymentGateway> _registry;

    public PaymentService(IEnumerable<IPaymentGateway> gateways)
    {
        _registry = gateways.ToDictionary(g => g.Method);
    }

    public Task<PaymentResult> ProcessAsync(PaymentMethod method, decimal amount, string idempotencyKey, CancellationToken ct = default)
    {
        if (!_registry.TryGetValue(method, out var gateway))
            return Task.FromResult(new PaymentResult(false, null, "METHOD_NOT_REGISTERED",
                $"Payment method {method} is not configured."));
        if (amount < 0)
            return Task.FromResult(new PaymentResult(false, null, "AMOUNT_INVALID",
                "Amount must be greater than or equal to zero."));
        return gateway.ProcessAsync(amount, idempotencyKey, ct);
    }

    public bool IsAllowedFor(UserType userType, PaymentMethod method) =>
        GetAllowedMethods(userType).Contains(method);

    public IEnumerable<PaymentMethod> GetAllowedMethods(UserType userType) => userType switch
    {
        UserType.LocalCommuter  => new[] { PaymentMethod.MoMo, PaymentMethod.VNPay, PaymentMethod.Cash },
        UserType.ForeignTourist => new[] { PaymentMethod.PayPal, PaymentMethod.ApplePay, PaymentMethod.Cash },
        UserType.Admin          => Array.Empty<PaymentMethod>(),
        _                       => Array.Empty<PaymentMethod>()
    };
}
