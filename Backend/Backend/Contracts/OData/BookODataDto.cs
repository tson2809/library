namespace Server.Contracts.OData;

public sealed class BookODataDto
{
    public int BookId { get; set; }

    public string Isbn { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Publisher { get; set; }

    public int? PublishedYear { get; set; }

    public int TotalCopies { get; set; }

    public int AvailableCopies { get; set; }

    public int BorrowCount { get; set; }

    public bool CanDeactivate { get; set; }
}
