using SaigonRide.Domain.Enums;

namespace SaigonRide.Services.Payment.Gateways;

public class PayPalGateway : SimulatedGatewayBase
{
    public override PaymentMethod Method => PaymentMethod.PayPal;
    protected override string Prefix => "PAYPAL";
}
