using System.ComponentModel.DataAnnotations;

namespace Server.Contracts.Members;

public sealed class CreateMemberRequest
{
    [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
    [StringLength(150)]
    public string FullName { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    [StringLength(255)]
    public string? Email { get; set; }

    [Required(ErrorMessage = "Số điện thoại không được để trống.")]
    [RegularExpression("^0\\d{9}$", ErrorMessage = "Số điện thoại phải bắt đầu bằng số 0 và gồm đúng 10 chữ số.")]
    public string Phone { get; set; } = string.Empty;

    [StringLength(500)]
    public string? AddressLine { get; set; }
}

public sealed class UpdateMemberRequest
{
    [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    [StringLength(255)]
    public string? Email { get; set; }

    [Required(ErrorMessage = "Số điện thoại không được để trống.")]
    [RegularExpression("^0\\d{9}$", ErrorMessage = "Số điện thoại phải bắt đầu bằng số 0 và gồm đúng 10 chữ số.")]
    public string Phone { get; set; } = string.Empty;

    [StringLength(500)]
    public string? AddressLine { get; set; }

    public bool? IsActive { get; set; }
}
