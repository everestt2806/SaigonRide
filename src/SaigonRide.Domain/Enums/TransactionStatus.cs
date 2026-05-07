namespace SaigonRide.Domain.Enums;

/// <summary>
/// State machine for payment transactions. Replaces the simple <c>IsPaid</c>
/// boolean with explicit states so the system can distinguish between
/// "payment not yet attempted" and "payment attempted but failed".
/// </summary>
public enum TransactionStatus
{
    /// <summary>Transaction row created, payment not yet initiated.</summary>
    Created = 0,

    /// <summary>User redirected to gateway; awaiting callback or IPN.</summary>
    Processing = 1,

    /// <summary>Gateway confirmed successful payment.</summary>
    Completed = 2,

    /// <summary>Gateway returned a definitive failure.</summary>
    Failed = 3,

    /// <summary>Previously completed payment has been refunded.</summary>
    Refunded = 4
}