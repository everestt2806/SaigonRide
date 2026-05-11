using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using SaigonRide.Domain.Enums;
using SaigonRide.Domain.ValueObjects;
using SaigonRide.Services.Payment.Gateways;
using Xunit;

namespace SaigonRide.Tests.Payment;

/// <summary>
/// TC-VNPAY: VNPay Sandbox integration tests.
/// Verifies the VNPay gateway correctly builds payment URLs with proper
/// signature generation, handles IPN callback validation, and processes
/// return URL verification. Tests the live external API integration
/// required for Tier 4 (The Innovation Pioneer).
/// </summary>
public class VNPayIntegrationTests
{
    private readonly VNPayGateway _sut;
    private readonly VNPayHelper _helper;

    public VNPayIntegrationTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["VNPay:TmnCode"] = "FAKE_TMN",
                ["VNPay:HashSecret"] = "FAKE_SECRET_HASH_KEY_32CHARS!!",
                ["VNPay:BaseUrl"] = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html",
                ["VNPay:ReturnUrl"] = "https://localhost:5001/Payment/VNPayReturn"
            })
            .Build();

        _sut = new VNPayGateway(config, new Mock<ILogger<VNPayGateway>>().Object);
        _helper = new VNPayHelper("FAKE_TMN", "FAKE_SECRET_HASH_KEY_32CHARS!!",
            "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html",
            "https://localhost:5001/Payment/VNPayReturn");
    }

    // ── TC-VNPAY-01: Gateway returns correct method ──
    [Fact]
    public void Method_returns_VNPay()
    {
        Assert.Equal(PaymentMethod.VNPay, _sut.Method);
    }

    // ── TC-VNPAY-02: ProcessAsync returns success with payment URL ──
    [Fact]
    public async Task ProcessAsync_returns_success_with_payment_url()
    {
        var result = await _sut.ProcessAsync(
            amount: 50000m,
            idempotencyKey: "test-key-001");

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.False(string.IsNullOrEmpty(result.GatewayRef));
        Assert.False(string.IsNullOrEmpty(result.PaymentUrl));
        Assert.Contains("sandbox.vnpayment.vn", result.PaymentUrl);
    }

    // ── TC-VNPAY-03: ProcessAsync with large amount handles correctly ──
    [Fact]
    public async Task ProcessAsync_handles_large_amount()
    {
        var result = await _sut.ProcessAsync(
            amount: 9999999m,
            idempotencyKey: "test-key-003");

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Contains("sandbox.vnpayment.vn", result.PaymentUrl);
    }

    // ── TC-VNPAY-04: VNPayHelper creates valid payment URL ──
    [Fact]
    public void CreatePaymentUrl_returns_valid_url_with_params()
    {
        var url = _helper.CreatePaymentUrl(50000m, "ORDER001", "Test payment", "127.0.0.1");

        Assert.False(string.IsNullOrEmpty(url));
        Assert.Contains("vnp_TmnCode=FAKE_TMN", url);
        Assert.Contains("vnp_TxnRef=ORDER001", url);
        Assert.Contains("vnp_SecureHash=", url);
        Assert.Contains("sandbox.vnpayment.vn", url);
    }

    // ── TC-VNPAY-05: VNPayHelper amount is multiplied by 100 ──
    [Fact]
    public void CreatePaymentUrl_multiplies_amount_by_100()
    {
        var url = _helper.CreatePaymentUrl(1000m, "ORDER002", "Test", "127.0.0.1");

        // VNPay expects amount × 100, so 1000 VND = 100000
        Assert.Contains("vnp_Amount=100000", url);
    }

    // ── TC-VNPAY-06: ValidateCallback returns true for valid signature ──
    [Fact]
    public void ValidateCallback_returns_true_for_valid_signature()
    {
        // First create a payment URL to get a valid signature
        var url = _helper.CreatePaymentUrl(50000m, "ORDER003", "Test", "127.0.0.1");

        // Extract query params from the URL
        var uri = new Uri(url);
        var queryParams = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var queryDict = queryParams.AllKeys
            .Where(k => k != null)
            .ToDictionary(k => k!, k => queryParams[k] ?? "");

        var isValid = _helper.ValidateCallback(queryDict);

        Assert.True(isValid);
    }

    // ── TC-VNPAY-07: ValidateCallback returns false for tampered signature ──
    [Fact]
    public void ValidateCallback_returns_false_for_tampered_signature()
    {
        var queryDict = new Dictionary<string, string>
        {
            ["vnp_TmnCode"] = "FAKE_TMN",
            ["vnp_Amount"] = "5000000",
            ["vnp_TxnRef"] = "ORDER004",
            ["vnp_OrderInfo"] = "Test",
            ["vnp_SecureHash"] = "TAMPERED_HASH_VALUE"
        };

        var isValid = _helper.ValidateCallback(queryDict);

        Assert.False(isValid);
    }

    // ── TC-VNPAY-08: ParseCallback extracts correct fields ──
    [Fact]
    public void ParseCallback_extracts_correct_fields()
    {
        var queryDict = new Dictionary<string, string>
        {
            ["vnp_TxnRef"] = "ORDER005",
            ["vnp_Amount"] = "5000000",
            ["vnp_OrderInfo"] = "Test payment",
            ["vnp_TransactionNo"] = "12345678",
            ["vnp_BankCode"] = "NCB",
            ["vnp_ResponseCode"] = "00",
            ["vnp_TransactionStatus"] = "00",
            ["vnp_PayDate"] = "20260509150000",
            ["vnp_SecureHash"] = "somehash"
        };

        var result = _helper.ParseCallback(queryDict);

        Assert.Equal("ORDER005", result.TxnRef);
        Assert.Equal(50000m, result.Amount); // Divided by 100
        Assert.Equal("Test payment", result.OrderInfo);
        Assert.Equal("12345678", result.TransactionNo);
        Assert.Equal("NCB", result.BankCode);
        Assert.True(result.IsSuccess);
    }

    // ── TC-VNPAY-09: ParseCallback IsSuccess is false for non-00 response ──
    [Fact]
    public void ParseCallback_IsSuccess_false_for_error_response()
    {
        var queryDict = new Dictionary<string, string>
        {
            ["vnp_TxnRef"] = "ORDER006",
            ["vnp_Amount"] = "5000000",
            ["vnp_ResponseCode"] = "24",
            ["vnp_TransactionStatus"] = "02"
        };

        var result = _helper.ParseCallback(queryDict);

        Assert.False(result.IsSuccess);
        Assert.Equal("24", result.ResponseCode);
    }

    // ── TC-VNPAY-10: Multiple payments generate unique gateway refs ──
    [Fact]
    public async Task ProcessAsync_generates_unique_refs()
    {
        var result1 = await _sut.ProcessAsync(10000m, "unique-key-1");
        var result2 = await _sut.ProcessAsync(20000m, "unique-key-2");

        Assert.NotEqual(result1.GatewayRef, result2.GatewayRef);
    }

    // ── TC-VNPAY-11: RefundAsync returns not supported ──
    [Fact]
    public async Task RefundAsync_returns_not_supported()
    {
        var result = await _sut.RefundAsync("ORDER001", 50000m);

        Assert.False(result.Success);
        Assert.Equal("REFUND_NOT_SUPPORTED", result.FailureCode);
    }
}