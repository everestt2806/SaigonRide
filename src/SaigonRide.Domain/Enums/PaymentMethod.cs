namespace SaigonRide.Domain.Enums;

/// <summary>
/// Five concrete payment strategies registered through <c>IPaymentGateway</c>
/// (D-04). Local users can use MoMo/VNPay/Cash; tourists can use PayPal/ApplePay/Cash.
/// </summary>
public enum PaymentMethod
{
    Cash = 1,
    MoMo = 2,
    VNPay = 3,
    PayPal = 4,
    ApplePay = 5
}
