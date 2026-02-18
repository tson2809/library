namespace Server.Options;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "LibraryServer";

    public string Audience { get; set; } = "LibraryClient";

    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 480;
}
