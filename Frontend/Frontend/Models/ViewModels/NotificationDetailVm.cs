namespace Client_web.Models.ViewModels;

public sealed class NotificationDetailVm
{
    public int NotificationId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public string CreatedByUserName { get; set; } = string.Empty;

    public string CreatedByFullName { get; set; } = string.Empty;

    public bool SendToAll { get; set; }

    public List<string> Recipients { get; set; } = new();

    public bool IsRead { get; set; }

    public DateTime? ReadAt { get; set; }
}
