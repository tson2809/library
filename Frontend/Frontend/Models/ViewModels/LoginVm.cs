using System.ComponentModel.DataAnnotations;

namespace Client_web.Models.ViewModels;

public sealed class LoginVm
{
    [Display(Name = "Tên đăng nhập")]
    [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập.")]
    [MaxLength(50)]
    public string Username { get; set; } = string.Empty;

    [Display(Name = "Mật khẩu")]
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}

public sealed class LoginUserVm
{
    public int UserId { get; set; }

    public string Username { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public string? AvatarUrl { get; set; }
}

