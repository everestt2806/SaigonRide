using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SaigonRide.Domain.Enums;
using SaigonRide.Domain.ValueObjects;

namespace SaigonRide.Services.Payment.Gateways;

/// <summary>
/// Live VNPay Sandbox gateway. Builds a real VNPay payment URL using HMAC-SHA512
/// signing per VNPay v2.1.0 spec. In sandbox mode, the URL points to
/// https://sandbox.vnpayment.vn/paymentv2/vpcpay.html.
/// </summary>
public class VNPayGateway : IPaymentGateway
{
    private readonly VNPayHelper _helper;
    private readonly ILogger<VNPayGateway> _logger;

    public VNPayGateway(IConfiguration config, ILogger<VNPayGateway> _logger)
    {
        this._logger = _logger;
        var tmnCode = config["VNPay:TmnCode"] ?? "FAKE_TMN";
        var hashSecret = config["VNPay:HashSecret"] ?? "FAKE_SECRET";
        var baseUrl = config["VNPay:BaseUrl"] ?? "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
        var returnUrl = config["VNPay:ReturnUrl"] ?? "https://localhost:5001/Payment/VNPayReturn";
        _helper = new VNPayHelper(tmnCode, hashSecret, baseUrl, returnUrl);
    }

    public PaymentMethod Method => PaymentMethod.VNPay;

    public Task<PaymentResult> ProcessAsync(decimal amount, string idempotencyKey, CancellationToken ct = default)
    {
        // Generate a unique order ID from the idempotency key
        var sanitized = idempotencyKey.Replace("-", "");
        var orderId = sanitized.Length > 20 ? sanitized.Substring(0, 20) : sanitized;
        var orderInfo = $"SaigonRide rental payment - {orderId}";
        var ipAddress = "127.0.0.1"; // Sandbox default

        try
        {
            var paymentUrl = _helper.CreatePaymentUrl(amount, orderId, orderInfo, ipAddress);

            _logger.LogInformation("VNPay payment URL created for order {OrderId}, amount {Amount} VND", orderId, amount);

            // Return success with PaymentUrl so the controller can redirect the user
            return Task.FromResult(new PaymentResult(
                Success: true,
                GatewayRef: orderId,
                PaymentUrl: paymentUrl,
                RawResponse: $"VNPay sandbox URL generated for order {orderId}"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "VNPay payment URL creation failed for order {OrderId}", orderId);
            return Task.FromResult(new PaymentResult(
                Success: false,
                GatewayRef: null,
                FailureCode: "VNPAY_ERROR",
                FailureMessage: $"VNPay error: {ex.Message}"));
        }
    }

    public Task<PaymentResult> RefundAsync(string gatewayRef, decimal amount, CancellationToken ct = default)
    {
        // VNPay sandbox does not support automated refunds; log and return not-supported
        _logger.LogWarning("VNPay refund requested for {GatewayRef} but not supported in sandbox", gatewayRef);
        return Task.FromResult(new PaymentResult(
            Success: false,
            GatewayRef: gatewayRef,
            FailureCode: "REFUND_NOT_SUPPORTED",
            FailureMessage: "VNPay sandbox does not support automated refunds."));
    }

    /// <summary>
    /// Verify a VNPay callback (IPN or Return) and extract the result.
    /// Called from PaymentController.
    /// </summary>
    public VNPayCallbackResult VerifyCallback(Dictionary<string, string> query)
    {
        var isValid = _helper.ValidateCallback(query);
        if (!isValid)
        {
            _logger.LogWarning("VNPay callback signature validation failed");
            return new VNPayCallbackResult { ResponseCode = "97", TransactionStatus = "97" };
        }
        return _helper.ParseCallback(query);
    }
}