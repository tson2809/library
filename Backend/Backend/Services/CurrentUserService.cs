using System.Security.Claims;

namespace Server.Services;

public sealed class CurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public ClaimsPrincipal User => _httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal(new ClaimsIdentity());

    public bool IsAuthenticated => User.Identity?.IsAuthenticated == true;

    public string? Username => User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("username");

    public int? UserId
    {
        get
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            return int.TryParse(raw, out var userId) ? userId : null;
        }
    }

    public string? RoleName => User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role");
}
