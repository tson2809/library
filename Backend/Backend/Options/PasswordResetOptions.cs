namespace Server.Options;

public sealed class PasswordResetOptions
{
    public int TokenMinutes { get; set; } = 30;
}
