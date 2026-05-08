namespace SaigonRide.Services.Auth;

/// <summary>
/// Abstraction so the Data seeder and tests can hash passwords without
/// dragging in the BCrypt assembly directly.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string plainText);
    bool Verify(string plainText, string hash);
}
