using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace SaigonRide.Data.Converters;

/// <summary>
/// EF Core value converter that encrypts a string column at rest using
/// AES-256-CBC + HMAC-SHA256 (encrypt-then-MAC). Implements NFR-03 for
/// <c>ForeignTouristDetails.PassportNumber</c>. Refer to §7.3.1 row NFR-03.
/// </summary>
/// <remarks>
/// Key material is derived from <c>Security:EncryptionKey</c> in
/// <c>appsettings.json</c> (32-byte base64 string). Format on disk:
/// <c>iv (16 bytes) || ciphertext || hmac (32 bytes)</c> all base64-encoded.
/// </remarks>
public sealed class AesEncryptedString : ValueConverter<string, string>
{
    public AesEncryptedString(byte[] aesKey, byte[] hmacKey)
        : base(
            plain => Encrypt(plain, aesKey, hmacKey),
            cipher => Decrypt(cipher, aesKey, hmacKey))
    {
    }

    private static string Encrypt(string plain, byte[] aesKey, byte[] hmacKey)
    {
        if (string.IsNullOrEmpty(plain)) return string.Empty;
        using var aes = Aes.Create();
        aes.Key = aesKey;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.GenerateIV();
        var iv = aes.IV;
        var cipherBytes = aes.EncryptCbc(Encoding.UTF8.GetBytes(plain), iv);

        using var hmac = new HMACSHA256(hmacKey);
        var payload = new byte[iv.Length + cipherBytes.Length];
        Buffer.BlockCopy(iv, 0, payload, 0, iv.Length);
        Buffer.BlockCopy(cipherBytes, 0, payload, iv.Length, cipherBytes.Length);
        var tag = hmac.ComputeHash(payload);

        var output = new byte[payload.Length + tag.Length];
        Buffer.BlockCopy(payload, 0, output, 0, payload.Length);
        Buffer.BlockCopy(tag, 0, output, payload.Length, tag.Length);
        return Convert.ToBase64String(output);
    }

    private static string Decrypt(string cipher, byte[] aesKey, byte[] hmacKey)
    {
        if (string.IsNullOrEmpty(cipher)) return string.Empty;
        var bytes = Convert.FromBase64String(cipher);
        if (bytes.Length < 16 + 32)
            throw new CryptographicException("Encrypted payload is too short.");

        var tagOffset = bytes.Length - 32;
        var payload = new byte[tagOffset];
        var tag = new byte[32];
        Buffer.BlockCopy(bytes, 0, payload, 0, tagOffset);
        Buffer.BlockCopy(bytes, tagOffset, tag, 0, 32);

        using var hmac = new HMACSHA256(hmacKey);
        var expected = hmac.ComputeHash(payload);
        if (!CryptographicOperations.FixedTimeEquals(expected, tag))
            throw new CryptographicException("HMAC validation failed.");

        var iv = new byte[16];
        var cipherBytes = new byte[payload.Length - 16];
        Buffer.BlockCopy(payload, 0, iv, 0, 16);
        Buffer.BlockCopy(payload, 16, cipherBytes, 0, cipherBytes.Length);

        using var aes = Aes.Create();
        aes.Key = aesKey;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        var plain = aes.DecryptCbc(cipherBytes, iv);
        return Encoding.UTF8.GetString(plain);
    }

    public static byte[] DeriveAesKey(string keyMaterial)
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes(keyMaterial + "::aes"));
    }

    public static byte[] DeriveHmacKey(string keyMaterial)
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes(keyMaterial + "::hmac"));
    }
}
