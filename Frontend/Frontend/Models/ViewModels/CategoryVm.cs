namespace Client_web.Models.ViewModels;

public sealed class CategoryVm
{
    public int CategoryId { get; set; }

    public string CategoryName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int BookCount { get; set; }
}
