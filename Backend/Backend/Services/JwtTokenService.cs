using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Server.Contracts.Auth;
using Server.Options;

namespace Server.Services;

public sealed class JwtTokenService
{
    private readonly JwtOptions _options;

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    public (string Token, DateTime ExpiresAt) CreateToken(UserSummaryDto user)
    {
        if (string.IsNullOrWhiteSpace(_options.SigningKey) || Encoding.UTF8.GetByteCount(_options.SigningKey) < 32)
        {
            throw new InvalidOperationException("Jwt:SigningKey phải có tối thiểu 32 bytes.");
        }

        var now = DateTime.UtcNow;
        var expiresAt = now.AddMinutes(_options.AccessTokenMinutes <= 0 ? 480 : _options.AccessTokenMinutes);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.RoleName),
            new("username", user.Username),
            new("full_name", user.FullName),
            new("role", user.RoleName),
            new("role_display", user.Role)
        };

        if (!string.IsNullOrWhiteSpace(user.AvatarUrl))
        {
            claims.Add(new Claim("avatar_url", user.AvatarUrl));
        }

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: expiresAt,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
