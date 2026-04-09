using System.ComponentModel.DataAnnotations;

namespace Server.Contracts.Categories;

public sealed class CreateCategoryRequest
{
    [Required(ErrorMessage = "Vui lòng nhập tên thể loại.")]
    [StringLength(100, ErrorMessage = "Tên thể loại không được vượt quá 100 ký tự.")]
    public string CategoryName { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }
}
