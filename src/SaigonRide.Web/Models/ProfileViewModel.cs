using System.ComponentModel.DataAnnotations;
using SaigonRide.Domain.Enums;

namespace SaigonRide.Web.Models;

public class ProfileViewModel
{
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Họ và tên / Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Display(Name = "Loại tài khoản / Account Type")]
    public UserType UserType { get; set; }

    // Read-only fields
    [Display(Name = "CCCD / National ID")]
    public string? NationalId { get; set; }

    [Display(Name = "Hộ chiếu / Passport")]
    public string? PassportNumber { get; set; }

    [Display(Name = "Quốc tịch / Nationality")]
    public string? Nationality { get; set; }

    // Editable fields
    [Display(Name = "Số điện thoại / Phone Number")]
    public string? PhoneNumber { get; set; }

    // Password change fields
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu hiện tại / Current Password")]
    public string? CurrentPassword { get; set; }

    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu mới / New Password")]
    [MinLength(6, ErrorMessage = "Mật khẩu mới phải dài ít nhất 6 ký tự / New password must be at least 6 characters")]
    public string? NewPassword { get; set; }

    [DataType(DataType.Password)]
    [Display(Name = "Xác nhận mật khẩu mới / Confirm New Password")]
    [Compare("NewPassword", ErrorMessage = "Mật khẩu xác nhận không khớp / The new password and confirmation password do not match.")]
    public string? ConfirmNewPassword { get; set; }
}
