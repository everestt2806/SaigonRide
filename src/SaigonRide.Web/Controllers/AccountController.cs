using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaigonRide.Domain.Entities;
using SaigonRide.Services.Auth;
using SaigonRide.Web.Infrastructure;
using SaigonRide.Web.Models;

namespace SaigonRide.Web.Controllers;

public class AccountController : Controller
{
    private readonly IAuthService _authService;
    private readonly IConfiguration _config;

    public AccountController(IAuthService authService, IConfiguration config)
    {
        _authService = authService;
        _config = config;
    }

    private string Scheme => _config["Security:CookieAuthenticationScheme"] ?? CookieAuthenticationDefaults.AuthenticationScheme;

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);
        var result = await _authService.LoginAsync(vm.Email, vm.Password);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Invalid email or password.");
            return View(vm);
        }
        await SignInUserAsync(result.Value!, vm.RememberMe);
        return SafeRedirect(vm.ReturnUrl);
    }

    [HttpGet]
    public IActionResult Register() => View();

    [HttpGet]
    public IActionResult RegisterLocal() => View();

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterLocal(RegisterLocalViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);
        var result = await _authService.RegisterLocalAsync(new RegisterLocalDto(vm.Email, vm.FullName, vm.Password, vm.NationalId, vm.PhoneNumber));
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Registration failed.");
            return View(vm);
        }
        await SignInUserAsync(result.Value!, isPersistent: false);
        TempData["success"] = "Welcome to SaigonRide!";
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult RegisterTourist() => View();

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterTourist(RegisterTouristViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);
        var result = await _authService.RegisterTouristAsync(new RegisterTouristDto(vm.Email, vm.FullName, vm.Password, vm.PassportNumber, vm.Nationality));
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Registration failed.");
            return View(vm);
        }
        await SignInUserAsync(result.Value!, isPersistent: false);
        TempData["success"] = "Welcome to SaigonRide! Your passport is encrypted.";
        return RedirectToAction("Index", "Home");
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(Scheme);
        return RedirectToAction("Index", "Home");
    }

    [HttpGet, Authorize]
    public async Task<IActionResult> Profile([FromServices] SaigonRide.Data.Repositories.IUserRepository users)
    {
        var email = HttpContext.User.FindFirst(ClaimTypes.Email)?.Value;
        if (email == null) return RedirectToAction("Login");

        var user = await users.GetByEmailWithDetailsAsync(email);
        if (user == null) return RedirectToAction("Login");

        var vm = new ProfileViewModel
        {
            Email = user.Email,
            FullName = user.FullName,
            UserType = user.UserType,
            NationalId = user.LocalDetails?.NationalId,
            PhoneNumber = user.LocalDetails?.PhoneNumber,
            PassportNumber = user.TouristDetails?.PassportNumber,
            Nationality = user.TouristDetails?.Nationality
        };

        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize]
    public async Task<IActionResult> Profile(ProfileViewModel vm, [FromServices] SaigonRide.Data.Repositories.IUserRepository users)
    {
        var email = HttpContext.User.FindFirst(ClaimTypes.Email)?.Value;
        if (email == null) return RedirectToAction("Login");

        var user = await users.GetByEmailWithDetailsAsync(email);
        if (user == null) return RedirectToAction("Login");

        // Restore read-only properties since they won't be posted or we don't want to trust the client
        vm.Email = user.Email;
        vm.UserType = user.UserType;
        vm.NationalId = user.LocalDetails?.NationalId;
        vm.PassportNumber = user.TouristDetails?.PassportNumber;
        vm.Nationality = user.TouristDetails?.Nationality;

        if (!ModelState.IsValid)
        {
            return View(vm);
        }

        var dto = new UpdateProfileDto(vm.FullName, vm.PhoneNumber, vm.CurrentPassword, vm.NewPassword);
        var result = await _authService.UpdateProfileAsync(user.Id, dto);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Cập nhật thất bại / Update failed.");
            return View(vm);
        }

        TempData["success"] = "Hồ sơ đã được cập nhật thành công / Profile updated successfully.";
        
        // If password changed, maybe sign out? But here we just keep them signed in since Cookie is still valid, 
        // but wait, if we change claims like FullName we should re-sign-in.
        if (user.FullName != vm.FullName)
        {
            await SignInUserAsync(result.Value!, isPersistent: false);
        }

        return RedirectToAction(nameof(Profile));
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();

    [HttpPost]
    public IActionResult SwitchLang(string culture, string? returnUrl = null)
    {
        if (string.IsNullOrEmpty(culture) || (culture != "en" && culture != "vi" && culture != "ko"))
            culture = "en";
        Response.Cookies.Append(
            Microsoft.AspNetCore.Localization.CookieRequestCultureProvider.DefaultCookieName,
            Microsoft.AspNetCore.Localization.CookieRequestCultureProvider.MakeCookieValue(
                new Microsoft.AspNetCore.Localization.RequestCulture(culture)),
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddDays(30), HttpOnly = false });
        return SafeRedirect(returnUrl);
    }

    private async Task SignInUserAsync(User user, bool isPersistent)
        {
            var claims = new List<Claim>
            {
                new(CurrentUser.ClaimUserId, user.Id.ToString()),
                new(ClaimTypes.Email, user.Email),
                new(ClaimTypes.Name, user.FullName),
                new(CurrentUser.ClaimUserType, user.UserType.ToString()),
                new(ClaimTypes.Role, user.UserType.ToString())
            };
            var identity = new ClaimsIdentity(claims, Scheme);
            var principal = new ClaimsPrincipal(identity);

            // M-3: RememberMe → persistent cookie 30 days; otherwise session cookie.
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = isPersistent
            };
            if (isPersistent)
                authProperties.ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30);
            // else: ExpiresUtc stays null → session cookie (browser-close expiry).

            await HttpContext.SignInAsync(Scheme, principal, authProperties);

            // L-1: Only set the culture cookie on first visit — respect the user's
            // explicit choice if they already switched via the language toggle.
            if (!Request.Cookies.ContainsKey(Microsoft.AspNetCore.Localization
                    .CookieRequestCultureProvider.DefaultCookieName))
            {
                string roleCulture = user.UserType == Domain.Enums.UserType.LocalCommuter ? "vi" : "en";
                Response.Cookies.Append(
                    Microsoft.AspNetCore.Localization.CookieRequestCultureProvider.DefaultCookieName,
                    Microsoft.AspNetCore.Localization.CookieRequestCultureProvider.MakeCookieValue(
                        new Microsoft.AspNetCore.Localization.RequestCulture(roleCulture)),
                    new CookieOptions { Expires = DateTimeOffset.UtcNow.AddDays(30), HttpOnly = false }
                );
            }
        }

    private IActionResult SafeRedirect(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return RedirectToAction("Index", "Home");
    }
}
