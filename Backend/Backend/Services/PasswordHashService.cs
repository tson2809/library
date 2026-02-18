using System.Security.Cryptography;

namespace Server.Services;

public sealed class PasswordHashService
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 210_000;
    private const string Prefix = "PBKDF2-SHA256";

    public string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return string.Join('$', Prefix, Iterations, Convert.ToBase64String(salt), Convert.ToBase64String(hash));
    }

    public PasswordVerificationResult VerifyPassword(string password, string storedHash)
    {
        var normalizedStoredHash = storedHash.Trim();
        if (IsLegacySha256(normalizedStoredHash))
        {
            var legacyHash = ComputeSha256(password);
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(legacyHash),
                Convert.FromHexString(normalizedStoredHash))
                    ? PasswordVerificationResult.ValidLegacyHash
                    : PasswordVerificationResult.Invalid;
        }

        var parts = normalizedStoredHash.Split('$');
        if (parts.Length != 4 || parts[0] != Prefix || !int.TryParse(parts[1], out var iterations))
        {
            return PasswordVerificationResult.Invalid;
        }

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expectedHash = Convert.FromBase64String(parts[3]);
            var actualHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expectedHash.Length);
            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash)
                ? PasswordVerificationResult.Valid
                : PasswordVerificationResult.Invalid;
        }
        catch (FormatException)
        {
            return PasswordVerificationResult.Invalid;
        }
    }

    private static bool IsLegacySha256(string storedHash)
    {
        return storedHash.Length == 64 && storedHash.All(Uri.IsHexDigit);
    }

    private static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

public enum PasswordVerificationResult
{
    Invalid,
    Valid,
    ValidLegacyHash
}
