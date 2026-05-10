using Microsoft.AspNetCore.Localization;
using SaigonRide.Web.Infrastructure;

namespace SaigonRide.Web.Infrastructure;

public class UserRoleCultureProvider : RequestCultureProvider
{
    public override Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
    {
        if (httpContext == null)
            throw new ArgumentNullException(nameof(httpContext));

        if (!httpContext.User.Identity?.IsAuthenticated ?? true)
        {
            // Anonymous -> English
            return Task.FromResult<ProviderCultureResult?>(new ProviderCultureResult("en"));
        }

        var userTypeStr = httpContext.User.FindFirst(CurrentUser.ClaimUserType)?.Value;
        if (string.IsNullOrEmpty(userTypeStr))
        {
            return Task.FromResult<ProviderCultureResult?>(new ProviderCultureResult("en"));
        }

        if (Enum.TryParse<Domain.Enums.UserType>(userTypeStr, out var userType))
        {
            if (userType == Domain.Enums.UserType.LocalCommuter)
            {
                // Local -> Vietnamese
                return Task.FromResult<ProviderCultureResult?>(new ProviderCultureResult("vi"));
            }
            // Tourist or Admin -> English
            return Task.FromResult<ProviderCultureResult?>(new ProviderCultureResult("en"));
        }

        return Task.FromResult<ProviderCultureResult?>(new ProviderCultureResult("en"));
    }
}
