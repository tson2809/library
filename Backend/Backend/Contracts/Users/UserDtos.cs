using System.ComponentModel.DataAnnotations;

namespace Server.Contracts.Users;

public sealed class CreateUserRequest
{
    [Required(ErrorMessage = "Tên đăng nhập không được để trống.")]
    [StringLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Họ tên không được để trống.")]
    [StringLength(150)]
    public string FullName { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    [StringLength(255)]
    public string? Email { get; set; }

    [RegularExpression("^0\\d{9}$", ErrorMessage = "Số điện thoại phải bắt đầu bằng số 0 và gồm đúng 10 chữ số.")]
    public string? Phone { get; set; }

    [Required(ErrorMessage = "Vai trò không được để trống.")]
    [RegularExpression("^(manager|staff)$", ErrorMessage = "Vai trò không hợp lệ.")]
    public string RoleName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mật khẩu không được để trống.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự.")]
    public string Password { get; set; } = string.Empty;
}

public sealed class UpdateUserRequest
{
    [Required(ErrorMessage = "Họ tên không được để trống.")]
    [StringLength(150)]
    public string FullName { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    [StringLength(255)]
    public string? Email { get; set; }

    [RegularExpression("^0\\d{9}$", ErrorMessage = "Số điện thoại phải bắt đầu bằng số 0 và gồm đúng 10 chữ số.")]
    public string? Phone { get; set; }

    [RegularExpression("^(manager|staff)$", ErrorMessage = "Vai trò không hợp lệ.")]
    public string? RoleName { get; set; }

    public bool? IsActive { get; set; }
}

public sealed class ResetPasswordRequest
{
    [Required(ErrorMessage = "Mật khẩu mới không được để trống.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu mới phải có ít nhất 6 ký tự.")]
    public string NewPassword { get; set; } = string.Empty;
}
