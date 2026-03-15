using System.ComponentModel.DataAnnotations;

namespace Server.Contracts.Books;

public sealed class CreateBookRequest
{
    [Required(ErrorMessage = "ISBN không được để trống.")]
    [RegularExpression("^\\d{13}$", ErrorMessage = "ISBN phải gồm đúng 13 chữ số.")]
    public string Isbn { get; set; } = string.Empty;

    [Required(ErrorMessage = "Tên sách không được để trống.")]
    [StringLength(255, ErrorMessage = "Tên sách không được vượt quá 255 ký tự.")]
    public string Title { get; set; } = string.Empty;

    [StringLength(255)]
    public string? PublisherName { get; set; }

    [Range(1000, 9999, ErrorMessage = "Năm xuất bản không hợp lệ.")]
    public int? PublishedYear { get; set; }

    [StringLength(500)]
    public string? ImageUrl { get; set; }

    public List<string> AuthorNames { get; set; } = new();

    public List<string> CategoryNames { get; set; } = new();

    [Range(1, 99999, ErrorMessage = "Số lượng bản sao phải lớn hơn 0.")]
    public int InitialCopies { get; set; } = 1;
}

public sealed class UpdateBookRequest
{
    [Required(ErrorMessage = "ISBN không được để trống.")]
    [RegularExpression("^\\d{13}$", ErrorMessage = "ISBN phải gồm đúng 13 chữ số.")]
    public string Isbn { get; set; } = string.Empty;

    [Required(ErrorMessage = "Tên sách không được để trống.")]
    [StringLength(255, ErrorMessage = "Tên sách không được vượt quá 255 ký tự.")]
    public string Title { get; set; } = string.Empty;

    [StringLength(255)]
    public string? PublisherName { get; set; }

    [Range(1000, 9999, ErrorMessage = "Năm xuất bản không hợp lệ.")]
    public int? PublishedYear { get; set; }

    [StringLength(500)]
    public string? ImageUrl { get; set; }

    public List<string> AuthorNames { get; set; } = new();

    public List<string> CategoryNames { get; set; } = new();

    [Range(1, 99999, ErrorMessage = "Số lượng bản sao phải lớn hơn 0.")]
    public int DesiredTotalCopies { get; set; }
}

public sealed class UpdateBookCopyStatusRequest
{
    [Required(ErrorMessage = "Vui lòng nhập trạng thái bản sao.")]
    [StringLength(30)]
    public string CopyStatus { get; set; } = string.Empty;

    [StringLength(30)]
    public string? PhysicalCondition { get; set; }

    [StringLength(50)]
    public string? LocationCode { get; set; }
}
