using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Server.Services;

public sealed class IsbnLookupService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public IsbnLookupService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public async Task<IsbnLookupResult?> LookupAsync(string isbn, CancellationToken cancellationToken)
    {
        var normalized = NormalizeIsbn(isbn);
        if (string.IsNullOrEmpty(normalized))
        {
            return null;
        }

        var fromIsbnDb = await TryLookupIsbnDbAsync(normalized, cancellationToken);
        if (fromIsbnDb is not null)
        {
            return fromIsbnDb;
        }

        var fromGoogle = await TryLookupGoogleBooksAsync(normalized, cancellationToken);
        if (fromGoogle is not null)
        {
            return fromGoogle;
        }

        var fromOpenLibrary = await TryLookupOpenLibraryAsync(normalized, cancellationToken);
        if (fromOpenLibrary is not null)
        {
            return fromOpenLibrary;
        }

        return await TryLookupOpenLibrarySearchAsync(normalized, cancellationToken);
    }

    private async Task<IsbnLookupResult?> TryLookupIsbnDbAsync(string isbn, CancellationToken cancellationToken)
    {
        var apiKey = _configuration["IsbnLookup:IsbnDbApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        try
        {
            using var client = CreateClient();
            client.DefaultRequestHeaders.TryAddWithoutValidation("x-api-key", apiKey);

            var url = $"https://api.isbndb.com/book/{Uri.EscapeDataString(isbn)}";
            await using var stream = await client.GetStreamAsync(url, cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (!doc.RootElement.TryGetProperty("book", out var bookEl)
                || bookEl.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var resultIsbn = ReadString(bookEl, "isbn13") ?? ReadString(bookEl, "isbn");
            if (!string.IsNullOrWhiteSpace(resultIsbn)
                && !string.Equals(NormalizeIsbn(resultIsbn), isbn, StringComparison.Ordinal))
            {
                return null;
            }

            var imageUrl = ReadString(bookEl, "image")
                ?? ReadString(bookEl, "image_original");
            var result = new IsbnLookupResult
            {
                Source = "isbnDb",
                Isbn = isbn,
                Title = ReadString(bookEl, "title_long") ?? ReadString(bookEl, "title") ?? string.Empty,
                PublisherName = ReadString(bookEl, "publisher"),
                PublishedDate = NormalizePublishedDate(
                    ReadString(bookEl, "date_published") ?? ReadString(bookEl, "publish_date")),
                Description = ReadString(bookEl, "overview") ?? ReadString(bookEl, "synopsis"),
                Authors = ReadStringArray(bookEl, "authors"),
                Categories = ReadStringArray(bookEl, "subjects").Take(8).ToList(),
                ImageUrl = NormalizeImageUrl(imageUrl)
            };

            return string.IsNullOrWhiteSpace(result.Title) ? null : result;
        }
        catch
        {
            return null;
        }
    }

    private async Task<IsbnLookupResult?> TryLookupGoogleBooksAsync(string isbn, CancellationToken cancellationToken)
    {
        try
        {
            using var client = CreateClient();
            var url = $"https://www.googleapis.com/books/v1/volumes?q=isbn:{isbn}&maxResults=1&printType=books&country=VN";
            var apiKey = _configuration["IsbnLookup:GoogleBooksApiKey"];
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                url += $"&key={Uri.EscapeDataString(apiKey)}";
            }

            await using var stream = await client.GetStreamAsync(url, cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (!doc.RootElement.TryGetProperty("totalItems", out var totalEl) || totalEl.GetInt32() <= 0)
            {
                return null;
            }

            if (!doc.RootElement.TryGetProperty("items", out var itemsEl)
                || itemsEl.ValueKind != JsonValueKind.Array
                || itemsEl.GetArrayLength() == 0)
            {
                return null;
            }

            var info = itemsEl[0].GetProperty("volumeInfo");
            if (!GoogleBookMatchesIsbn(info, isbn))
            {
                return null;
            }

            var imageUrl = ExtractFirstString(info, "imageLinks", "thumbnail")
                ?? ExtractFirstString(info, "imageLinks", "smallThumbnail");

            var result = new IsbnLookupResult
            {
                Source = "googleBooks",
                Isbn = isbn,
                Title = ReadString(info, "title") ?? string.Empty,
                Subtitle = ReadString(info, "subtitle"),
                PublisherName = ReadString(info, "publisher"),
                PublishedDate = NormalizePublishedDate(ReadString(info, "publishedDate")),
                Description = ReadString(info, "description"),
                Authors = ReadStringArray(info, "authors"),
                Categories = ReadStringArray(info, "categories"),
                ImageUrl = NormalizeImageUrl(imageUrl)
            };

            return string.IsNullOrWhiteSpace(result.Title) ? null : result;
        }
        catch
        {
            return null;
        }
    }

    private async Task<IsbnLookupResult?> TryLookupOpenLibraryAsync(string isbn, CancellationToken cancellationToken)
    {
        try
        {
            using var client = CreateClient();
            var url = $"https://openlibrary.org/api/books?bibkeys=ISBN:{isbn}&format=json&jscmd=data";
            await using var stream = await client.GetStreamAsync(url, cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (!doc.RootElement.TryGetProperty($"ISBN:{isbn}", out var bookEl))
            {
                return null;
            }

            var publisher = bookEl.TryGetProperty("publishers", out var pubs)
                && pubs.ValueKind == JsonValueKind.Array
                && pubs.GetArrayLength() > 0
                ? ReadString(pubs[0], "name")
                : null;

            var authors = bookEl.TryGetProperty("authors", out var au) && au.ValueKind == JsonValueKind.Array
                ? au.EnumerateArray()
                    .Select(a => ReadString(a, "name"))
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s!)
                    .ToList()
                : new List<string>();

            var categories = bookEl.TryGetProperty("subjects", out var sub) && sub.ValueKind == JsonValueKind.Array
                ? sub.EnumerateArray()
                    .Select(a => ReadString(a, "name"))
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s!)
                    .Take(8)
                    .ToList()
                : new List<string>();

            var imageUrl = ExtractFirstString(bookEl, "cover", "large")
                ?? ExtractFirstString(bookEl, "cover", "medium")
                ?? ExtractFirstString(bookEl, "cover", "small");

            var result = new IsbnLookupResult
            {
                Source = "openLibrary",
                Isbn = isbn,
                Title = ReadString(bookEl, "title") ?? string.Empty,
                Subtitle = ReadString(bookEl, "subtitle"),
                PublisherName = publisher,
                PublishedDate = NormalizePublishedDate(ReadString(bookEl, "publish_date")),
                Description = ReadString(bookEl, "notes"),
                Authors = authors,
                Categories = categories,
                ImageUrl = NormalizeImageUrl(imageUrl)
            };

            return string.IsNullOrWhiteSpace(result.Title) ? null : result;
        }
        catch
        {
            return null;
        }
    }

    private async Task<IsbnLookupResult?> TryLookupOpenLibrarySearchAsync(string isbn, CancellationToken cancellationToken)
    {
        try
        {
            using var client = CreateClient();
            var url = "https://openlibrary.org/search.json"
                + $"?isbn={Uri.EscapeDataString(isbn)}"
                + "&limit=1&fields=title,author_name,publisher,first_publish_year,isbn,cover_i,subject";
            await using var stream = await client.GetStreamAsync(url, cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (!doc.RootElement.TryGetProperty("docs", out var docsEl)
                || docsEl.ValueKind != JsonValueKind.Array
                || docsEl.GetArrayLength() == 0)
            {
                return null;
            }

            var bookEl = docsEl[0];
            if (!OpenLibrarySearchBookMatchesIsbn(bookEl, isbn))
            {
                return null;
            }

            var coverId = ReadInt32(bookEl, "cover_i");
            var result = new IsbnLookupResult
            {
                Source = "openLibrary",
                Isbn = isbn,
                Title = ReadString(bookEl, "title") ?? string.Empty,
                PublisherName = ReadStringArray(bookEl, "publisher").FirstOrDefault(),
                PublishedDate = NormalizePublishedDate(ReadInt32(bookEl, "first_publish_year")?.ToString(CultureInfo.InvariantCulture)),
                Authors = ReadStringArray(bookEl, "author_name"),
                Categories = ReadStringArray(bookEl, "subject").Take(8).ToList(),
                ImageUrl = coverId is null ? null : $"https://covers.openlibrary.org/b/id/{coverId}-L.jpg"
            };

            return string.IsNullOrWhiteSpace(result.Title) ? null : result;
        }
        catch
        {
            return null;
        }
    }

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(8);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        client.DefaultRequestHeaders.UserAgent.ParseAdd("LibraryISBNLookup/1.0");
        return client;
    }

    private static bool GoogleBookMatchesIsbn(JsonElement volumeInfo, string isbn)
    {
        if (!volumeInfo.TryGetProperty("industryIdentifiers", out var identifiers)
            || identifiers.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var identifier in identifiers.EnumerateArray())
        {
            var value = ReadString(identifier, "identifier");
            if (string.Equals(NormalizeIsbn(value ?? string.Empty), isbn, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool OpenLibrarySearchBookMatchesIsbn(JsonElement bookEl, string isbn)
    {
        return ReadStringArray(bookEl, "isbn")
            .Any(value => string.Equals(NormalizeIsbn(value), isbn, StringComparison.Ordinal));
    }

    private static string NormalizeIsbn(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var digits = new string(raw.Where(char.IsDigit).ToArray());
        return digits.Length is 10 or 13 ? digits : string.Empty;
    }

    private static string? NormalizePublishedDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var formats = new[] { "yyyy", "yyyy-MM", "yyyy-MM-dd", "MMM d, yyyy", "MMMM d, yyyy", "MMM yyyy", "MMMM yyyy" };
        if (DateTime.TryParseExact(raw, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
        {
            return parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        return null;
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var prop)
            && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;
    }

    private static int? ReadInt32(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var prop)
            && prop.ValueKind == JsonValueKind.Number
            && prop.TryGetInt32(out var value)
            ? value
            : null;
    }

    private static List<string> ReadStringArray(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(propertyName, out var arr)
            || arr.ValueKind != JsonValueKind.Array)
        {
            return new List<string>();
        }

        return arr.EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => x.GetString() ?? string.Empty)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
    }

    private static string? ExtractFirstString(JsonElement element, string parentName, string childName)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(parentName, out var parent)
            || parent.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return ReadString(parent, childName);
    }

    private static string? NormalizeImageUrl(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return null;
        }

        if (imageUrl.StartsWith("//", StringComparison.Ordinal))
        {
            return $"https:{imageUrl}";
        }

        return imageUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            ? string.Concat("https://", imageUrl.AsSpan("http://".Length))
            : imageUrl;
    }
}

public sealed class IsbnLookupResult
{
    public string Source { get; set; } = string.Empty;
    public string Isbn { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string? PublisherName { get; set; }
    public string? PublishedDate { get; set; }
    public string? Description { get; set; }
    public List<string> Authors { get; set; } = new();
    public List<string> Categories { get; set; } = new();
    public string? ImageUrl { get; set; }
}
