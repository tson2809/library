namespace Client_web.Models.ViewModels;

public sealed class NotificationListItemVm
{
    public int NotificationId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Preview { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public string CreatedByUserName { get; set; } = string.Empty;

    public string CreatedByFullName { get; set; } = string.Empty;

    public bool SendToAll { get; set; }

    public bool IsRead { get; set; }
}
