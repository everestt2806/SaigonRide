using System.Text.RegularExpressions;
using SaigonRide.Data.Converters;
using SaigonRide.Domain.Entities;
using SaigonRide.Services.Auth;
using Xunit;

namespace SaigonRide.Tests.Security;

/// <summary>
/// TC-SEC-01..03: NFR-02 (BCrypt cost 12), NFR-03 (AES-256 + HMAC for
/// passport storage and masking).
/// </summary>
public class SecurityTests
{
    [Fact] // TC-SEC-01
    public void Bcrypt_hash_uses_cost_factor_12()
    {
        var hasher = new BcryptPasswordHasher();
        var hash = hasher.Hash("supersecret");

        Assert.Matches(new Regex(@"^\$2[abxy]\$12\$"), hash);
        Assert.True(hasher.Verify("supersecret", hash));
        Assert.False(hasher.Verify("wrong-password", hash));
    }

    [Fact] // TC-SEC-02
    public void Aes_round_trip_keeps_plaintext_secret()
    {
        const string keyMaterial = "test-aes-256-key-material-saigonride-tc-sec-02";
        var aes = AesEncryptedString.DeriveAesKey(keyMaterial);
        var hmac = AesEncryptedString.DeriveHmacKey(keyMaterial);
        var converter = new AesEncryptedString(aes, hmac);

        const string plain = "US2024-12345678";
        var cipher = (string)converter.ConvertToProvider(plain)!;
        var roundTrip = (string)converter.ConvertFromProvider(cipher)!;

        Assert.NotEqual(plain, cipher);
        Assert.Equal(plain, roundTrip);
        Assert.True(cipher.Length > plain.Length);
    }

    [Theory] // TC-SEC-03
    [InlineData("US2024-12345678", "***********5678")]
    [InlineData("AB12", "****")]
    [InlineData("X1", "**")]
    public void Receipt_view_masks_passport_to_last_four_digits(string passport, string expected)
    {
        var details = new ForeignTouristDetails { PassportNumber = passport, Nationality = "US" };
        Assert.Equal(expected, details.GetMaskedPassport());
    }
}
