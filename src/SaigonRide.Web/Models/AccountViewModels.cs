using System.ComponentModel.DataAnnotations;

namespace SaigonRide.Web.Models;

public class LoginViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
    public bool RememberMe { get; set; }
}

public class RegisterLocalViewModel
{
    [Required, EmailAddress, StringLength(120)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(120)]
    public string FullName { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
    public string Password { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required, StringLength(20, MinimumLength = 9, ErrorMessage = "National ID must be 9–20 characters.")]
    [Display(Name = "National ID")]
    public string NationalId { get; set; } = string.Empty;

    [Phone, StringLength(20)]
    [Display(Name = "Phone Number")]
    public string? PhoneNumber { get; set; }
}

public class RegisterTouristViewModel
{
    [Required, EmailAddress, StringLength(120)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(120)]
    public string FullName { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
    public string Password { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required, StringLength(64, MinimumLength = 4)]
    [Display(Name = "Passport Number")]
    public string PassportNumber { get; set; } = string.Empty;

    [Required, StringLength(60)]
    public string Nationality { get; set; } = string.Empty;
}
