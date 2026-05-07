using System.ComponentModel.DataAnnotations;

namespace SaigonRide.Domain.Entities;

/// <summary>
/// 1-to-0..1 sub-table; <see cref="PassportNumber"/> is encrypted via an EF
/// value converter (AES-256, NFR-03). The receipt view masks the value to the
/// last 4 digits using <see cref="GetMaskedPassport"/>.
/// </summary>
public class ForeignTouristDetails
{
    public int UserId { get; set; }

    /// <summary>Passport plain text — encrypted at rest by the EF converter.</summary>
    [Required, MaxLength(64)]
    public string PassportNumber { get; set; } = string.Empty;

    [Required, MaxLength(60)]
    public string Nationality { get; set; } = string.Empty;

    public User? User { get; set; }

    /// <summary>Returns "****1234" — last 4 digits, used by the receipt view (NFR-03).</summary>
    public string GetMaskedPassport()
    {
        if (string.IsNullOrEmpty(PassportNumber))
            return string.Empty;
        return PassportNumber.Length <= 4
            ? new string('*', PassportNumber.Length)
            : new string('*', PassportNumber.Length - 4) + PassportNumber[^4..];
    }
}
