using System.ComponentModel.DataAnnotations;

namespace Server.Contracts.Notifications;

public sealed class CreateNotificationRequest
{
    [Required(ErrorMessage = "Vui lòng nhập tiêu đề thông báo.")]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập nội dung thông báo.")]
    [StringLength(4000)]
    public string Content { get; set; } = string.Empty;

    public bool SendToAll { get; set; }

    public List<string> RecipientUsernames { get; set; } = new();
}
