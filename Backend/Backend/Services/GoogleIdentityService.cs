using System.Text.Json;
using Google.Apis.Auth;
using Microsoft.Extensions.Options;
using Server.Options;

namespace Server.Services;

public sealed record GoogleIdentityPayload(string Email, bool EmailVerified);

public sealed class GoogleIdentityService
{
    private readonly GoogleAuthOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;

    public GoogleIdentityService(IOptions<GoogleAuthOptions> options, IHttpClientFactory httpClientFactory)
    {
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<GoogleIdentityPayload?> ValidateIdTokenAsync(string idToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId))
        {
            throw new InvalidOperationException("Thiếu cấu hình GoogleAuth:ClientId.");
        }

        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { _options.ClientId }
            });

            return new GoogleIdentityPayload(payload.Email, payload.EmailVerified);
        }
        catch (InvalidJwtException)
        {
            return await ValidateWithTokenInfoAsync(idToken, cancellationToken);
        }
    }

    private async Task<GoogleIdentityPayload?> ValidateWithTokenInfoAsync(string idToken, CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient();
        using var response = await httpClient.GetAsync(
            $"https://oauth2.googleapis.com/tokeninfo?id_token={Uri.EscapeDataString(idToken)}",
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;

        var audience = GetString(root, "aud");
        if (!string.Equals(audience, _options.ClientId, StringComparison.Ordinal))
        {
            return null;
        }

        var email = GetString(root, "email");
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        return new GoogleIdentityPayload(email, GetBoolean(root, "email_verified"));
    }

    private static string? GetString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.ToString(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            _ => null
        };
    }

    private static bool GetBoolean(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.TryParse(property.GetString(), out var parsed) && parsed,
            _ => false
        };
    }
}
