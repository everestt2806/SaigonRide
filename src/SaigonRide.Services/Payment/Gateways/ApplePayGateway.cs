using SaigonRide.Domain.Enums;

namespace SaigonRide.Services.Payment.Gateways;

public class ApplePayGateway : SimulatedGatewayBase
{
    public override PaymentMethod Method => PaymentMethod.ApplePay;
    protected override string Prefix => "APPLEPAY";
}
