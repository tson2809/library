namespace Client_web.Models.ViewModels;

public sealed class NotificationComposeVm
{
    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public bool SendToAll { get; set; }

    public List<string> RecipientUsernames { get; set; } = new();
}
