using SaigonRide.Domain.Entities;
using Xunit;

namespace SaigonRide.Tests.Domain;

/// <summary>
/// TC-SEC-02: Verify passport masking for receipt display (NFR-03).
/// </summary>
public class ForeignTouristDetailsTests
{
    [Theory]
    [InlineData("AB1234567", "*****4567")]
    [InlineData("12345678", "****5678")]
    [InlineData("7890", "****")]
    [InlineData("12", "**")]
    [InlineData("A", "*")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void GetMaskedPassport_shows_only_last_4_digits(string? passport, string expected)
    {
        var details = new ForeignTouristDetails
        {
            PassportNumber = passport ?? string.Empty
        };

        var masked = details.GetMaskedPassport();

        Assert.Equal(expected, masked);
    }
}
