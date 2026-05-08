namespace SaigonRide.Services.Auth;

/// <summary>
/// BCrypt.Net-Next implementation with cost factor 12, satisfying NFR-02
/// ("bcrypt cost ≥ 12, plain-text never stored").
/// </summary>
public class BcryptPasswordHasher : IPasswordHasher
{
    public const int WorkFactor = 12;

    public string Hash(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            throw new ArgumentException("Password must not be empty.", nameof(plainText));
        return BCrypt.Net.BCrypt.HashPassword(plainText, WorkFactor);
    }

    public bool Verify(string plainText, string hash)
    {
        if (string.IsNullOrEmpty(plainText) || string.IsNullOrEmpty(hash))
            return false;
        try
        {
            return BCrypt.Net.BCrypt.Verify(plainText, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return false;
        }
    }
}
