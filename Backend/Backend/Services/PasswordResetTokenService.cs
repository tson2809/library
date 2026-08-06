using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Server.Options;

namespace Server.Services;

public sealed class PasswordResetTokenService
{
    private readonly ConcurrentDictionary<string, PasswordResetTokenEntry> _tokens = new(StringComparer.Ordinal);
    private readonly PasswordResetOptions _options;

    public PasswordResetTokenService(IOptions<PasswordResetOptions> options)
    {
        _options = options.Value;
    }

    public string CreateToken(string email)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
        var normalizedEmail = NormalizeEmail(email);
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(_options.TokenMinutes <= 0 ? 30 : _options.TokenMinutes);
        _tokens[token] = new PasswordResetTokenEntry(normalizedEmail, expiresAt);
        return token;
    }

    public bool ValidateAndConsume(string email, string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        if (!_tokens.TryRemove(token, out var entry))
        {
            return false;
        }

        if (entry.ExpiresAt < DateTimeOffset.UtcNow)
        {
            return false;
        }

        return string.Equals(entry.Email, NormalizeEmail(email), StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeEmail(string email)
    {
        return (email ?? string.Empty).Trim().ToLowerInvariant();
    }

    private sealed record PasswordResetTokenEntry(string Email, DateTimeOffset ExpiresAt);
}
