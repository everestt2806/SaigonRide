using System.Security.Claims;
using SaigonRide.Domain.Enums;

namespace SaigonRide.Web.Infrastructure;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    int? Id { get; }
    string? Email { get; }
    UserType? UserType { get; }
    bool IsAdmin { get; }
}

public class CurrentUser : ICurrentUser
{
    public const string ClaimUserId = ClaimTypes.NameIdentifier;
    public const string ClaimUserType = "saigonride:user-type";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor) => _httpContextAccessor = httpContextAccessor;

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;

    public int? Id
    {
        get
        {
            var raw = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimUserId)?.Value;
            return int.TryParse(raw, out var id) ? id : null;
        }
    }

    public string? Email => _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Email)?.Value;

    public UserType? UserType
    {
        get
        {
            var raw = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimUserType)?.Value;
            return Enum.TryParse<UserType>(raw, out var t) ? t : null;
        }
    }

    public bool IsAdmin => UserType == Domain.Enums.UserType.Admin;
}
