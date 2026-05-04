namespace Client_web.Models.ViewModels;

public sealed class BookCopyLookupVm
{
    public int BookCopyId { get; set; }

    public string Barcode { get; set; } = string.Empty;

    public string CopyStatus { get; set; } = string.Empty;

    public string? PhysicalCondition { get; set; }

    public string? LocationCode { get; set; }

    public int BookId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Isbn { get; set; } = string.Empty;

    public string? ReservedForMemberCode { get; set; }

    public string? ReservedForMemberName { get; set; }

    public List<string> Authors { get; set; } = new();
}
