using System.ComponentModel.DataAnnotations;

namespace Server.Contracts.Auth;

public sealed class LoginRequest
{
    [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập.")]
    [StringLength(50, ErrorMessage = "Tên đăng nhập không được vượt quá 50 ký tự.")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    public string Password { get; set; } = string.Empty;
}

public sealed class GoogleLoginRequest
{
    [Required(ErrorMessage = "Thiếu mã đăng nhập Google.")]
    public string IdToken { get; set; } = string.Empty;
}

public sealed class UserSummaryDto
{
    public int UserId { get; set; }

    public string Username { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public string RoleName { get; set; } = string.Empty;

    public string? AvatarUrl { get; set; }
}

public sealed class LoginResponse
{
    public string AccessToken { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public UserSummaryDto User { get; set; } = new();
}

public sealed class UpdateProfileRequest
{
    [Required(ErrorMessage = "Tên đăng nhập không được để trống.")]
    [StringLength(50, ErrorMessage = "Tên đăng nhập không được vượt quá 50 ký tự.")]
    public string NewUsername { get; set; } = string.Empty;

    [RegularExpression("^0\\d{9}$", ErrorMessage = "Số điện thoại phải bắt đầu bằng số 0 và gồm đúng 10 chữ số.")]
    public string? Phone { get; set; }

    [StringLength(500)]
    public string? AvatarUrl { get; set; }
}

public sealed class ChangePasswordRequest
{
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu cũ.")]
    public string OldPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu mới phải có ít nhất 6 ký tự.")]
    public string NewPassword { get; set; } = string.Empty;
}

public sealed class ForgotPasswordRequest
{
    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Thiếu đường dẫn đặt lại mật khẩu.")]
    [Url(ErrorMessage = "Đường dẫn đặt lại mật khẩu không hợp lệ.")]
    public string ResetBaseUrl { get; set; } = string.Empty;
}

public sealed class ResetPasswordRequest
{
    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Thiếu mã đặt lại mật khẩu.")]
    public string Token { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu mới phải có ít nhất 6 ký tự.")]
    public string NewPassword { get; set; } = string.Empty;
}
