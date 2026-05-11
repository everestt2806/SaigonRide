using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using SaigonRide.Domain.Enums;
using SaigonRide.Services.Payment;
using SaigonRide.Services.Payment.Gateways;
using Xunit;

namespace SaigonRide.Tests.Payment;

/// <summary>
/// TC-PAY: dispatch + UserType filter for the Strategy registry (D-04).
/// </summary>
public class PaymentServiceTests
{
    private static IPaymentService BuildService()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["VNPay:TmnCode"] = "FAKE_TMN",
                ["VNPay:HashSecret"] = "FAKE_SECRET",
                ["VNPay:BaseUrl"] = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html",
                ["VNPay:ReturnUrl"] = "https://localhost:5001/Payment/VNPayReturn"
            })
            .Build();
        var logger = new Mock<ILogger<VNPayGateway>>().Object;

        return new PaymentService(new IPaymentGateway[]
        {
            new MoMoGateway(),
            new VNPayGateway(config, logger),
            new PayPalGateway(),
            new ApplePayGateway(),
            new CashGateway()
        });
    }

    [Theory] // TC-PAY-01..05
    [InlineData(PaymentMethod.MoMo,     "MOMO")]
    [InlineData(PaymentMethod.VNPay,    "TESTIDP")]
    [InlineData(PaymentMethod.PayPal,   "PAYPAL")]
    [InlineData(PaymentMethod.ApplePay, "APPLEPAY")]
    [InlineData(PaymentMethod.Cash,     "CASH")]
    public async Task Dispatch_routes_to_concrete_strategy(PaymentMethod method, string expectedPrefix)
    {
        var sut = BuildService();
        var result = await sut.ProcessAsync(method, 12_345m, "TEST-IDP");
        Assert.True(result.Success);
        Assert.NotNull(result.GatewayRef);
        Assert.StartsWith(expectedPrefix, result.GatewayRef);
    }

    [Fact] // TC-PAY-06
    public async Task Dispatch_fails_for_negative_amount()
    {
        var sut = BuildService();
        var result = await sut.ProcessAsync(PaymentMethod.MoMo, -1m, "TEST-IDP");
        Assert.False(result.Success);
        Assert.Equal("AMOUNT_INVALID", result.FailureCode);
    }

    [Theory] // TC-PAY-07..09 (Local user)
    [InlineData(PaymentMethod.MoMo,     true)]
    [InlineData(PaymentMethod.VNPay,    true)]
    [InlineData(PaymentMethod.Cash,     true)]
    [InlineData(PaymentMethod.PayPal,   false)]
    [InlineData(PaymentMethod.ApplePay, false)]
    public void Local_users_only_see_local_methods(PaymentMethod method, bool allowed)
    {
        var sut = BuildService();
        Assert.Equal(allowed, sut.IsAllowedFor(UserType.LocalCommuter, method));
    }

    [Theory] // TC-PAY-10..12 (Tourist user)
    [InlineData(PaymentMethod.PayPal,   true)]
    [InlineData(PaymentMethod.ApplePay, true)]
    [InlineData(PaymentMethod.Cash,     true)]
    [InlineData(PaymentMethod.MoMo,     false)]
    [InlineData(PaymentMethod.VNPay,    false)]
    public void Tourist_users_only_see_international_methods(PaymentMethod method, bool allowed)
    {
        var sut = BuildService();
        Assert.Equal(allowed, sut.IsAllowedFor(UserType.ForeignTourist, method));
    }
}
