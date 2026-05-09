using SaigonRide.Domain.Enums;

namespace SaigonRide.Services.Payment.Gateways;

public class MoMoGateway : SimulatedGatewayBase
{
    public override PaymentMethod Method => PaymentMethod.MoMo;
    protected override string Prefix => "MOMO";
}
