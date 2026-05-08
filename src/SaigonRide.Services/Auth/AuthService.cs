using SaigonRide.Data.Repositories;
using SaigonRide.Domain.Entities;
using SaigonRide.Domain.Enums;
using SaigonRide.Domain.ValueObjects;
using SaigonRide.Services.Audit;

namespace SaigonRide.Services.Auth;

public interface IAuthService
{
    Task<ServiceResult<User>> RegisterLocalAsync(RegisterLocalDto dto, CancellationToken ct = default);
    Task<ServiceResult<User>> RegisterTouristAsync(RegisterTouristDto dto, CancellationToken ct = default);
    Task<ServiceResult<User>> LoginAsync(string email, string password, CancellationToken ct = default);
    Task<ServiceResult<User>> UpdateProfileAsync(int userId, UpdateProfileDto dto, CancellationToken ct = default);
}

public record RegisterLocalDto(string Email, string FullName, string Password, string NationalId, string? PhoneNumber);
public record RegisterTouristDto(string Email, string FullName, string Password, string PassportNumber, string Nationality);
public record UpdateProfileDto(string FullName, string? PhoneNumber, string? CurrentPassword, string? NewPassword, string? NewEmail = null);

/// <summary>
/// Owns user registration and credential verification. Uses
/// <see cref="IPasswordHasher"/> for BCrypt cost-12 hashing (NFR-02) and the
/// AES-256 EF converter (NFR-03) for tourist passports.
/// </summary>
public class AuthService : IAuthService
{
    private const int MaxFailedAttempts = 5;
    private const int LockoutDurationMinutes = 15;

    private readonly IUserRepository _users;
    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher _hasher;
    private readonly IAuditLogger _audit;

    public AuthService(IUserRepository users, IUnitOfWork uow, IPasswordHasher hasher, IAuditLogger audit)
    {
        _users = users;
        _uow = uow;
        _hasher = hasher;
        _audit = audit;
    }

    public async Task<ServiceResult<User>> RegisterLocalAsync(RegisterLocalDto dto, CancellationToken ct = default)
    {
        var validation = ValidateBase(dto.Email, dto.Password);
        if (validation is not null) return validation;
        if (await _users.GetByEmailAsync(dto.Email, ct) is not null)
            return ServiceResult<User>.Fail("EMAIL_EXISTS", $"An account with email '{dto.Email}' already exists.");

        var user = new User
        {
            Email = dto.Email.Trim().ToLowerInvariant(),
            FullName = dto.FullName.Trim(),
            PasswordHash = _hasher.Hash(dto.Password),
            UserType = UserType.LocalCommuter,
            IsActive = true,
            LocalDetails = new LocalCommuterDetails
            {
                NationalId = dto.NationalId.Trim(),
                PhoneNumber = dto.PhoneNumber?.Trim()
            }
        };
        await _users.AddAsync(user, ct);
        await _uow.SaveChangesAsync(ct);
        await _audit.LogAsync("USER_REGISTERED", "User", user.Id.ToString(), null, new { user.Email, user.UserType }, ct);
        return ServiceResult<User>.Ok(user);
    }

    public async Task<ServiceResult<User>> RegisterTouristAsync(RegisterTouristDto dto, CancellationToken ct = default)
    {
        var validation = ValidateBase(dto.Email, dto.Password);
        if (validation is not null) return validation;
        if (await _users.GetByEmailAsync(dto.Email, ct) is not null)
            return ServiceResult<User>.Fail("EMAIL_EXISTS", $"An account with email '{dto.Email}' already exists.");

        var user = new User
        {
            Email = dto.Email.Trim().ToLowerInvariant(),
            FullName = dto.FullName.Trim(),
            PasswordHash = _hasher.Hash(dto.Password),
            UserType = UserType.ForeignTourist,
            IsActive = true,
            TouristDetails = new ForeignTouristDetails
            {
                PassportNumber = dto.PassportNumber.Trim(),
                Nationality = dto.Nationality.Trim()
            }
        };
        await _users.AddAsync(user, ct);
        await _uow.SaveChangesAsync(ct);
        await _audit.LogAsync("USER_REGISTERED", "User", user.Id.ToString(), null, new { user.Email, user.UserType }, ct);
        return ServiceResult<User>.Ok(user);
    }

    public async Task<ServiceResult<User>> LoginAsync(string email, string password, CancellationToken ct = default)
        {
            var user = await _users.GetByEmailWithDetailsAsync(email.Trim().ToLowerInvariant(), ct);
            if (user is null) return ServiceResult<User>.Fail("INVALID", "Invalid email or password.");
            if (!user.IsActive) return ServiceResult<User>.Fail("INACTIVE", "Account is deactivated.");

            // Check lockout (brute-force protection).
            if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTime.UtcNow)
            {
                var remaining = user.LockoutEnd.Value.Subtract(DateTime.UtcNow);
                var totalMinutes = (int)remaining.TotalMinutes;
                return ServiceResult<User>.Fail("LOCKED",
                    $"Account temporarily locked. Retry in {totalMinutes}m {remaining.Seconds}s.");
            }

            if (!_hasher.Verify(password, user.PasswordHash))
            {
                user.FailedAttempts++;
                if (user.FailedAttempts >= MaxFailedAttempts)
                {
                    user.LockoutEnd = DateTime.UtcNow.AddMinutes(LockoutDurationMinutes);
                    await _audit.LogAsync("USER_LOCKED", "User", user.Id.ToString(), null,
                        new { user.Email, FailedAttempts = user.FailedAttempts, LockoutMinutes = LockoutDurationMinutes }, ct);
                }
                await _uow.SaveChangesAsync(ct);
                return ServiceResult<User>.Fail("INVALID", "Invalid email or password.");
            }

            // Successful login — reset fail counters.
            if (user.FailedAttempts > 0 || user.LockoutEnd.HasValue)
            {
                user.FailedAttempts = 0;
                user.LockoutEnd = null;
                await _uow.SaveChangesAsync(ct);
            }

            return ServiceResult<User>.Ok(user);
        }

    public async Task<ServiceResult<User>> UpdateProfileAsync(int userId, UpdateProfileDto dto, CancellationToken ct = default)
        {
            var user = await _users.GetByIdAsync(userId, ct);
            if (user is null) return ServiceResult<User>.Fail("NOT_FOUND", "User not found.");

            if (!string.IsNullOrWhiteSpace(dto.FullName))
            {
                user.FullName = dto.FullName.Trim();
            }

            // M-1: Allow email update with uniqueness check.
            if (!string.IsNullOrWhiteSpace(dto.NewEmail) &&
                !string.Equals(dto.NewEmail.Trim(), user.Email, StringComparison.OrdinalIgnoreCase))
            {
                var normalized = dto.NewEmail.Trim().ToLowerInvariant();
                if (!normalized.Contains('@'))
                    return ServiceResult<User>.Fail("EMAIL_INVALID", "Email is invalid.");
                if (await _users.GetByEmailAsync(normalized, ct) is not null)
                    return ServiceResult<User>.Fail("EMAIL_EXISTS",
                        $"An account with email '{normalized}' already exists.");
                var oldEmail = user.Email;
                user.Email = normalized;
                await _audit.LogAsync("USER_EMAIL_CHANGED", "User", user.Id.ToString(), null,
                    new { OldEmail = oldEmail, NewEmail = normalized }, ct);
            }

        if (user.UserType == UserType.LocalCommuter && user.LocalDetails != null)
        {
            var oldPhone = user.LocalDetails.PhoneNumber;
            var newPhone = dto.PhoneNumber?.Trim();
            if (!string.Equals(oldPhone, newPhone, StringComparison.Ordinal))
            {
                user.LocalDetails.PhoneNumber = newPhone;
                await _audit.LogAsync("USER_PHONE_CHANGED", "User", user.Id.ToString(), null,
                    new { OldPhone = oldPhone, NewPhone = newPhone }, ct);
            }
        }

        if (!string.IsNullOrWhiteSpace(dto.NewPassword))
        {
            if (string.IsNullOrWhiteSpace(dto.CurrentPassword) || !_hasher.Verify(dto.CurrentPassword, user.PasswordHash))
            {
                return ServiceResult<User>.Fail("PASSWORD_MISMATCH", "Mật khẩu hiện tại không đúng / Current password is incorrect.");
            }
            if (dto.NewPassword.Length < 6)
            {
                return ServiceResult<User>.Fail("PASSWORD_WEAK", "Mật khẩu mới phải dài ít nhất 6 ký tự / New password must be at least 6 characters.");
            }
            user.PasswordHash = _hasher.Hash(dto.NewPassword);
        }

        await _uow.SaveChangesAsync(ct);
        await _audit.LogAsync("USER_UPDATED", "User", user.Id.ToString(), null, new { action = "ProfileUpdate" }, ct);
        return ServiceResult<User>.Ok(user);
    }

    private static ServiceResult<User>? ValidateBase(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            return ServiceResult<User>.Fail("EMAIL_INVALID", "Email is invalid.");
        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
            return ServiceResult<User>.Fail("PASSWORD_WEAK", "Password must be at least 6 characters.");
        return null;
    }
}
