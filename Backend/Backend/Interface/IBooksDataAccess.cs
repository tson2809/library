namespace Server.Interface;

public interface IBooksDataAccess
{
    Task<List<object>> GetBooksListAsync(CancellationToken cancellationToken);

    Task<int> GetMaxBarcodeAsync(CancellationToken cancellationToken);

    Task<List<object>> GetCategoriesListAsync(CancellationToken cancellationToken);

    Task<object?> GetBookDetailAsync(int bookId, CancellationToken cancellationToken);

    Task<List<object>> LookupBookCopiesAsync(string? query, CancellationToken cancellationToken);

    Task UpdateBookCopyStatusAsync(string barcode, string copyStatus, string? physicalCondition, string? locationCode, CancellationToken cancellationToken);

    Task<int> CreateBookAsync(string actorUsername, string isbn, string title, string? publisherName, int? publishedYear, string? imageUrl, IReadOnlyList<string> authorNames, IReadOnlyList<string> categoryNames, int initialCopies, CancellationToken cancellationToken);

    Task UpdateBookAsync(string actorUsername, int bookId, string isbn, string title, string? publisherName, int? publishedYear, string? imageUrl, IReadOnlyList<string> authorNames, IReadOnlyList<string> categoryNames, int desiredTotalCopies, CancellationToken cancellationToken);

    Task<int> CreateCategoryAsync(string actorUsername, string categoryName, string? description, CancellationToken cancellationToken);

    Task DeactivateBookAsync(string actorUsername, int bookId, CancellationToken cancellationToken);
}
