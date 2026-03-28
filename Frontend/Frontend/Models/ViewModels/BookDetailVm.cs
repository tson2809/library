namespace Client_web.Models.ViewModels;

public sealed class BookDetailVm
{
    public int BookId { get; set; }

    public string Isbn { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? ImageUrl { get; set; }

    public string? Publisher { get; set; }

    public int? PublishedYear { get; set; }

    public List<string> Authors { get; set; } = new();

    public List<string> Categories { get; set; } = new();

    public int TotalCopies { get; set; }

    public int AvailableCopies { get; set; }

    public List<BookCopyVm> Copies { get; set; } = new();
}

public sealed class BookCopyVm
{
    public int BookCopyId { get; set; }

    public string Barcode { get; set; } = string.Empty;

    public string CopyStatus { get; set; } = string.Empty;

    public string? PhysicalCondition { get; set; }

    public string? LocationCode { get; set; }
}
