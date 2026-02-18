using System.ComponentModel.DataAnnotations;

namespace Client_web.Models.ViewModels;

public sealed class ForgotPasswordVm
{
    [Display(Name = "Email")]
    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    public string Email { get; set; } = string.Empty;
}

public sealed class ResetPasswordVm
{
    [Required]
    public string Token { get; set; } = string.Empty;

    [Display(Name = "Email")]
    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Mật khẩu mới")]
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu mới phải có ít nhất 6 ký tự.")]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;

    [Display(Name = "Xác nhận mật khẩu mới")]
    [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu mới.")]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "Mật khẩu mới và xác nhận mật khẩu mới không khớp.")]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}
