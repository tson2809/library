namespace Server.Interface;

public interface IAuthDataAccess
{
    Task<(object? User, string? PasswordHash)> GetActiveUserForAuthenticationAsync(string username, CancellationToken cancellationToken);
    Task UpdateUserPasswordHashAsync(string username, string passwordHash, CancellationToken cancellationToken);
    Task<object?> AuthenticateByEmailAsync(string email, CancellationToken cancellationToken);
    Task<int?> GetUserIdByUsernameAsync(string username, CancellationToken cancellationToken);
    Task<int?> GetUserIdByEmailAsync(string email, CancellationToken cancellationToken);
    Task<string?> GetUsernameByEmailAsync(string email, CancellationToken cancellationToken);
    Task<string?> GetUserRoleNameAsync(string username, CancellationToken cancellationToken);
    Task<object?> GetUserProfileAsync(string username, CancellationToken cancellationToken);
    Task UpdateUserProfileAsync(string username, string? newUsername, string? phone, string? avatarUrl, CancellationToken cancellationToken);
    Task ChangePasswordAsync(string username, string newPasswordHash, CancellationToken cancellationToken);
    Task<List<object>> GetUsersListAsync(string actorUsername, CancellationToken cancellationToken);
    Task<int> CreateUserAsync(string actorUsername, string username, string fullName, string? email, string? phone, string roleName, string passwordHash, CancellationToken cancellationToken);
    Task UpdateUserAsync(string actorUsername, int userId, string? fullName, string? email, string? phone, string? roleName, bool? isActive, CancellationToken cancellationToken);
    Task ResetUserPasswordAsync(string actorUsername, int userId, string newPasswordHash, CancellationToken cancellationToken);
}
