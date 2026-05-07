namespace SaigonRide.Domain.ValueObjects;

/// <summary>
/// Output of an <c>IPaymentGateway.Process</c> call (CRC card 10). Used by
/// <c>PaymentService</c> to commit the transaction or roll back to checkout
/// (UC-02 E-4 / E-5).
/// </summary>
public record PaymentResult(
    bool Success,
    string? GatewayRef,
    string? FailureCode = null,
    string? FailureMessage = null);
