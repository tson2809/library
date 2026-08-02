using System;

namespace Server.Models;

public partial class NotificationRecipient
{
    public int NotificationRecipientId { get; set; }

    public int NotificationId { get; set; }

    public int RecipientUserId { get; set; }

    public bool IsRead { get; set; }

    public DateTime? ReadAt { get; set; }

    public virtual Notification Notification { get; set; } = null!;

    public virtual User RecipientUser { get; set; } = null!;
}
