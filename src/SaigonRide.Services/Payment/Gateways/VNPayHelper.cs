using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace SaigonRide.Services.Payment.Gateways;

/// <summary>
/// VNPay Sandbox / Production helper. Builds the payment URL with HMAC-SHA512
/// signature and verifies IPN / Return callbacks per VNPay v2.1.0 spec.
/// </summary>
public class VNPayHelper
{
    private readonly string _tmnCode;
    private readonly string _hashSecret;
    private readonly string _baseUrl;
    private readonly string _returnUrl;

    public VNPayHelper(string tmnCode, string hashSecret, string baseUrl, string returnUrl)
    {
        _tmnCode = tmnCode;
        _hashSecret = hashSecret;
        _baseUrl = baseUrl;
        _returnUrl = returnUrl;
    }

    /// <summary>
    /// Build the VNPay payment URL that the client should be redirected to.
    /// </summary>
    public string CreatePaymentUrl(decimal amount, string orderId, string orderInfo,
        string ipAddress, string locale = "vn", string currCode = "VND")
    {
        var vnpParams = new SortedList<string, string>(StringComparer.Ordinal)
        {
            ["vnp_Version"] = "2.1.0",
            ["vnp_Command"] = "pay",
            ["vnp_TmnCode"] = _tmnCode,
            ["vnp_Locale"] = locale,
            ["vnp_CurrCode"] = currCode,
            ["vnp_TxnRef"] = orderId,
            ["vnp_OrderInfo"] = orderInfo,
            ["vnp_OrderType"] = "other",
            ["vnp_Amount"] = ((long)(amount * 100)).ToString(), // VNPay expects amount × 100
            ["vnp_ReturnUrl"] = _returnUrl,
            ["vnp_IpAddr"] = ipAddress,
            ["vnp_CreateDate"] = DateTime.UtcNow.ToString("yyyyMMddHHmmss"),
        };

        var queryString = BuildQueryString(vnpParams);
        var secureHash = ComputeHmacSha512(_hashSecret, queryString);

        return $"{_baseUrl}?{queryString}&vnp_SecureHash={secureHash}";
    }

    /// <summary>
    /// Validate the secure hash returned by VNPay in IPN or Return callbacks.
    /// Accepts a flat dictionary of query parameters (framework-agnostic).
    /// </summary>
    public bool ValidateCallback(Dictionary<string, string> query)
    {
        if (!query.ContainsKey("vnp_SecureHash")) return false;

        var secureHash = query["vnp_SecureHash"];

        // Build a sorted param list excluding hash fields
        var vnpParams = new SortedList<string, string>(StringComparer.Ordinal);
        foreach (var kvp in query)
        {
            if (kvp.Key.StartsWith("vnp_SecureHash", StringComparison.OrdinalIgnoreCase)) continue;
            if (kvp.Key.StartsWith("vnp_SecureHashType", StringComparison.OrdinalIgnoreCase)) continue;

            if (!string.IsNullOrEmpty(kvp.Value))
                vnpParams[kvp.Key] = kvp.Value;
        }

        var queryString = BuildQueryString(vnpParams);
        var computedHash = ComputeHmacSha512(_hashSecret, queryString);

        return string.Equals(secureHash, computedHash, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Extract transaction details from a validated VNPay callback query.
    /// Accepts a flat dictionary of query parameters (framework-agnostic).
    /// </summary>
    public VNPayCallbackResult ParseCallback(Dictionary<string, string> query)
    {
        return new VNPayCallbackResult
        {
            TxnRef = query.GetValueOrDefault("vnp_TxnRef", ""),
            Amount = decimal.TryParse(query.GetValueOrDefault("vnp_Amount", "0"), out var amt) ? amt / 100m : 0m,
            OrderInfo = query.GetValueOrDefault("vnp_OrderInfo", ""),
            TransactionNo = query.GetValueOrDefault("vnp_TransactionNo", ""),
            BankCode = query.GetValueOrDefault("vnp_BankCode", ""),
            ResponseCode = query.GetValueOrDefault("vnp_ResponseCode", ""),
            TransactionStatus = query.GetValueOrDefault("vnp_TransactionStatus", ""),
            PayDate = query.GetValueOrDefault("vnp_PayDate", ""),
            SecureHash = query.GetValueOrDefault("vnp_SecureHash", "")
        };
    }

    private static string BuildQueryString(SortedList<string, string> params_)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < params_.Count; i++)
        {
            if (i > 0) sb.Append('&');
            sb.Append(WebUtility.UrlEncode(params_.Keys[i]));
            sb.Append('=');
            sb.Append(WebUtility.UrlEncode(params_.Values[i]));
        }
        return sb.ToString();
    }

    private static string ComputeHmacSha512(string key, string data)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var dataBytes = Encoding.UTF8.GetBytes(data);
        using var hmac = new HMACSHA512(keyBytes);
        var hash = hmac.ComputeHash(dataBytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

/// <summary>
/// Parsed VNPay callback parameters.
/// </summary>
public class VNPayCallbackResult
{
    public string TxnRef { get; set; } = "";
    public decimal Amount { get; set; }
    public string OrderInfo { get; set; } = "";
    public string TransactionNo { get; set; } = "";
    public string BankCode { get; set; } = "";
    public string ResponseCode { get; set; } = "";
    public string TransactionStatus { get; set; } = "";
    public string PayDate { get; set; } = "";
    public string SecureHash { get; set; } = "";

    /// <summary>True when ResponseCode == "00" (success per VNPay spec).</summary>
    public bool IsSuccess => ResponseCode == "00";
}