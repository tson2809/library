namespace Client_web.Models.ViewModels;

public sealed class ProfileVm
{
    public int UserId { get; set; }

    public string Username { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public bool IsActive { get; set; }

    public int RoleId { get; set; }

    public string Role { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public string? AvatarUrl { get; set; }
}

