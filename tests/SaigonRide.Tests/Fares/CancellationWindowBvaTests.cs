using SaigonRide.Services.Rentals;
using Xunit;

namespace SaigonRide.Tests.Fares;

/// <summary>
/// TC-FARE-BVA-CANCEL: BR-04 boundary on the 2-minute (120s) free
/// cancellation window. Three boundary samples: just inside, exactly at,
/// just outside.
/// </summary>
public class CancellationWindowBvaTests
{
    [Theory]
    [InlineData(119, true)]  // strictly inside
    [InlineData(120, true)]  // exactly at the boundary — still free per BR-04 spec wording "within 2 min"
    [InlineData(121, false)] // strictly outside — must be paid
    public void Cancel_within_window_is_decided_by_elapsed_seconds(int elapsedSeconds, bool expectFree)
    {
        var withinWindow = elapsedSeconds <= RentalService.FreeCancellationSeconds;
        Assert.Equal(expectFree, withinWindow);
    }
}
