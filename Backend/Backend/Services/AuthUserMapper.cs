using Server.Contracts.Auth;

namespace Server.Services;

public static class AuthUserMapper
{
    public static UserSummaryDto FromAuthenticationUser(object user, string? rawRoleName)
    {
        return new UserSummaryDto
        {
            UserId = ReadInt(user, "UserId"),
            Username = ReadString(user, "Username"),
            FullName = ReadString(user, "FullName"),
            Role = ReadString(user, "Role"),
            RoleName = NormalizeRoleName(rawRoleName),
            AvatarUrl = ReadNullableString(user, "AvatarUrl")
        };
    }

    private static string NormalizeRoleName(string? rawRoleName)
    {
        return (rawRoleName ?? string.Empty).Trim().ToLowerInvariant();
    }

    private static int ReadInt(object source, string propertyName)
    {
        var value = source.GetType().GetProperty(propertyName)?.GetValue(source);
        return value is int intValue ? intValue : 0;
    }

    private static string ReadString(object source, string propertyName)
    {
        return ReadNullableString(source, propertyName) ?? string.Empty;
    }

    private static string? ReadNullableString(object source, string propertyName)
    {
        return source.GetType().GetProperty(propertyName)?.GetValue(source) as string;
    }
}
