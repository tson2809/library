namespace Client_web.Models.ViewModels;

public sealed class SystemLogVm
{
    public int LogId { get; set; }

    public string ActionType { get; set; } = string.Empty;

    public string EntityName { get; set; } = string.Empty;

    public string? EntityId { get; set; }

    public string? Description { get; set; }

    public string? IpAddress { get; set; }

    public DateTime CreatedAt { get; set; }

    public string? UserName { get; set; }
}
