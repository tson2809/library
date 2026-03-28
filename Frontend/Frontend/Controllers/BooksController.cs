using System.Text.Json;
using Client_web.Models.ViewModels;
using Client_web.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;

namespace Client_web.Controllers;

public sealed class BooksController : Controller
{
    private readonly IBooksApiClient _booksApi;
    private readonly IBookCopiesApiClient _bookCopiesApi;
    private readonly ICategoriesApiClient _categoriesApi;
    private readonly IWebHostEnvironment _environment;
    private const string CatalogViewName = "Catalog";
    private const string CreateBookErrorKey = "CreateBook.Error";
    private const string CreateBookOpenModalKey = "CreateBook.OpenModal";
    private const string CreateBookIsbnKey = "CreateBook.Isbn";
    private const string CreateBookTitleKey = "CreateBook.Title";
    private const string CreateBookPublisherKey = "CreateBook.PublisherName";
    private const string CreateBookPublishedDateKey = "CreateBook.PublishedDate";
    private const string CreateBookAuthorNamesKey = "CreateBook.AuthorNames";
    private const string CreateBookCategoryNamesKey = "CreateBook.CategoryNames";
    private const string CreateBookInitialCopiesKey = "CreateBook.InitialCopies";

    public BooksController(IBooksApiClient booksApi, IBookCopiesApiClient bookCopiesApi, ICategoriesApiClient categoriesApi, IWebHostEnvironment environment)
    {
        _booksApi = booksApi;
        _bookCopiesApi = bookCopiesApi;
        _categoriesApi = categoriesApi;
        _environment = environment;
    }

    private bool IsManager()
    {
        var role = HttpContext.Session.GetString("Auth.Role");
        return string.Equals(role, "Quản lý", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryReadIntElement(JsonElement element, out int value)
    {
        value = default;

        if (element.ValueKind == JsonValueKind.Number)
        {
            return element.TryGetInt32(out value);
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            var raw = element.GetString();
            return int.TryParse(raw, out value);
        }

        return false;
    }

    private void PopulateCreateBookModalStateFromTempData()
    {
        ViewBag.CreateBookError = TempData[CreateBookErrorKey] as string;
        ViewBag.CreateBookOpenModal = string.Equals(TempData[CreateBookOpenModalKey] as string, "1", StringComparison.Ordinal);
        ViewBag.CreateBookIsbn = TempData[CreateBookIsbnKey] as string ?? string.Empty;
        ViewBag.CreateBookTitle = TempData[CreateBookTitleKey] as string ?? string.Empty;
        ViewBag.CreateBookPublisherName = TempData[CreateBookPublisherKey] as string ?? string.Empty;
        ViewBag.CreateBookPublishedDate = TempData[CreateBookPublishedDateKey] as string ?? string.Empty;
        ViewBag.CreateBookAuthorNames = TempData[CreateBookAuthorNamesKey] as string ?? string.Empty;
        ViewBag.CreateBookCategoryNames = TempData[CreateBookCategoryNamesKey] as string ?? string.Empty;

        var initialCopiesRaw = TempData[CreateBookInitialCopiesKey]?.ToString();
        ViewBag.CreateBookInitialCopies = int.TryParse(initialCopiesRaw, out var parsedInitialCopies) && parsedInitialCopies > 0
            ? parsedInitialCopies
            : 1;
    }

    private IActionResult RedirectCreateWithError(
        string message,
        string isbn,
        string title,
        string? publisherName,
        DateTime? publishedDate,
        string? authorNames,
        string? categoryNames,
        int initialCopies)
    {
        TempData[CreateBookErrorKey] = message;
        TempData[CreateBookOpenModalKey] = "1";
        TempData[CreateBookIsbnKey] = isbn;
        TempData[CreateBookTitleKey] = title;
        TempData[CreateBookPublisherKey] = publisherName?.Trim() ?? string.Empty;
        TempData[CreateBookPublishedDateKey] = publishedDate?.ToString("yyyy-MM-dd") ?? string.Empty;
        TempData[CreateBookAuthorNamesKey] = authorNames?.Trim() ?? string.Empty;
        TempData[CreateBookCategoryNamesKey] = categoryNames?.Trim() ?? string.Empty;
        TempData[CreateBookInitialCopiesKey] = initialCopies > 0 ? initialCopies.ToString() : "1";

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var response = await _booksApi.GetBooksAsync(cancellationToken);
        PopulateCreateBookModalStateFromTempData();
        if (!response.Success)
        {
            ViewBag.Error = UiTextLocalizer.TranslateMessage(response.Message);
            return View(CatalogViewName, new List<BookVm>());
        }

        var items = response.Data.Deserialize<List<BookVm>>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new List<BookVm>();

        if (IsManager())
        {
            var maxBarcodeResponse = await _booksApi.GetMaxBarcodeAsync(cancellationToken);
            if (maxBarcodeResponse.Success
                && maxBarcodeResponse.Data.ValueKind == JsonValueKind.Object
                && maxBarcodeResponse.Data.TryGetProperty("maxBarcode", out var maxBarcodeProperty)
                && TryReadIntElement(maxBarcodeProperty, out var maxBarcode))
            {
                ViewBag.MaxBarcode = maxBarcode;
            }
            else
            {
                ViewBag.MaxBarcode = 0;
            }
        }

        return View(CatalogViewName, items);
    }

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var response = await _booksApi.GetDetailAsync(id, cancellationToken);
        if (!response.Success)
        {
            TempData["Error"] = UiTextLocalizer.TranslateMessage(response.Message);
            return RedirectToAction(nameof(Index));
        }

        var item = response.Data.Deserialize<BookDetailVm>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (item is null)
        {
            TempData["Error"] = "Không đọc được dữ liệu chi tiết sách.";
            return RedirectToAction(nameof(Index));
        }

        if (IsManager())
        {
            var maxBarcodeResponse = await _booksApi.GetMaxBarcodeAsync(cancellationToken);
            if (maxBarcodeResponse.Success
                && maxBarcodeResponse.Data.ValueKind == JsonValueKind.Object
                && maxBarcodeResponse.Data.TryGetProperty("maxBarcode", out var maxBarcodeProperty)
                && TryReadIntElement(maxBarcodeProperty, out var maxBarcode))
            {
                ViewBag.MaxBarcode = maxBarcode;
            }
            else
            {
                ViewBag.MaxBarcode = 0;
            }

            var categoriesResponse = await _categoriesApi.GetCategoriesAsync(cancellationToken);
            if (categoriesResponse.Success)
            {
                var allCategories = categoriesResponse.Data.Deserialize<List<CategoryVm>>(new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new List<CategoryVm>();

                ViewBag.AllCategories = allCategories
                    .Select(c => c.CategoryName)
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(c => c)
                    .ToList();
            }
            else
            {
                ViewBag.AllCategories = new List<string>();
            }
        }

        return View(item);
    }

    [HttpGet]
    public async Task<IActionResult> LookupIsbn(string isbn, CancellationToken cancellationToken)
    {
        if (!IsManager())
        {
            return Json(new { success = false, message = "Bạn không có quyền tra cứu ISBN." });
        }

        var actorUsername = HttpContext.Session.GetString("Auth.Username") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(actorUsername))
        {
            return Json(new { success = false, message = "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại." });
        }

        var normalizedIsbn = isbn?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedIsbn))
        {
            return Json(new { success = false, message = "Vui lòng nhập ISBN." });
        }

        var response = await _booksApi.LookupIsbnAsync(normalizedIsbn, cancellationToken);

        if (!response.Success)
        {
            return Json(new { success = false, message = UiTextLocalizer.TranslateMessage(response.Message) });
        }

        return Json(new { success = true, message = response.Message, data = response.Data });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string isbn, string title, string? publisherName, DateTime? publishedDate, IFormFile? imageFile, string? authorNames, string? categoryNames, int initialCopies, CancellationToken cancellationToken)
    {
        var normalizedIsbn = isbn?.Trim() ?? string.Empty;
        var normalizedTitle = title?.Trim() ?? string.Empty;

        if (!IsManager())
        {
            return RedirectCreateWithError("Bạn không có quyền thêm sách.", normalizedIsbn, normalizedTitle, publisherName, publishedDate, authorNames, categoryNames, initialCopies);
        }

        if (string.IsNullOrWhiteSpace(normalizedIsbn) || string.IsNullOrWhiteSpace(normalizedTitle))
        {
            return RedirectCreateWithError("Vui lòng nhập ISBN và tên sách.", normalizedIsbn, normalizedTitle, publisherName, publishedDate, authorNames, categoryNames, initialCopies);
        }

        if (!Regex.IsMatch(normalizedIsbn, "^\\d{13}$"))
        {
            return RedirectCreateWithError("ISBN phải gồm đúng 13 chữ số.", normalizedIsbn, normalizedTitle, publisherName, publishedDate, authorNames, categoryNames, initialCopies);
        }

        if (initialCopies <= 0)
        {
            return RedirectCreateWithError("Số lượng bản sao phải lớn hơn 0.", normalizedIsbn, normalizedTitle, publisherName, publishedDate, authorNames, categoryNames, initialCopies);
        }

        if (publishedDate.HasValue && publishedDate.Value.Date > DateTime.Today)
        {
            return RedirectCreateWithError("Ngày xuất bản không được lớn hơn ngày hiện tại.", normalizedIsbn, normalizedTitle, publisherName, publishedDate, authorNames, categoryNames, initialCopies);
        }

        var maxBarcodeResponse = await _booksApi.GetMaxBarcodeAsync(cancellationToken);
        var maxBarcode = 0;
        if (maxBarcodeResponse.Success
            && maxBarcodeResponse.Data.ValueKind == JsonValueKind.Object
            && maxBarcodeResponse.Data.TryGetProperty("maxBarcode", out var maxBarcodeProperty)
            && TryReadIntElement(maxBarcodeProperty, out var parsedMaxBarcode))
        {
            maxBarcode = parsedMaxBarcode;
        }

        if (maxBarcode + initialCopies > 99999)
        {
            return RedirectCreateWithError("Số lượng bản sao vượt quá giới hạn mã vạch 5 chữ số.", normalizedIsbn, normalizedTitle, publisherName, publishedDate, authorNames, categoryNames, initialCopies);
        }

        var actorUsername = HttpContext.Session.GetString("Auth.Username") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(actorUsername))
        {
            return RedirectToAction("Login", "Auth");
        }

        string? imageUrl = null;
        if (imageFile is not null && imageFile.Length > 0)
        {
            var extension = Path.GetExtension(imageFile.FileName)?.ToLowerInvariant();
            var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".webp" };
            if (string.IsNullOrWhiteSpace(extension) || !allowedExtensions.Contains(extension))
            {
                return RedirectCreateWithError("Chỉ hỗ trợ ảnh định dạng PNG, JPG, JPEG hoặc WEBP.", normalizedIsbn, normalizedTitle, publisherName, publishedDate, authorNames, categoryNames, initialCopies);
            }

            const long maxBytes = 5 * 1024 * 1024;
            if (imageFile.Length > maxBytes)
            {
                return RedirectCreateWithError("Kích thước ảnh tối đa là 5MB.", normalizedIsbn, normalizedTitle, publisherName, publishedDate, authorNames, categoryNames, initialCopies);
            }

            var booksDirectory = Path.Combine(_environment.WebRootPath, "images", "books");
            Directory.CreateDirectory(booksDirectory);

            var fileName = $"book-{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(booksDirectory, fileName);

            await using (var stream = System.IO.File.Create(filePath))
            {
                await imageFile.CopyToAsync(stream, cancellationToken);
            }

            imageUrl = $"/images/books/{fileName}";
        }

        var parsedAuthors = (authorNames ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        var parsedCategories = (categoryNames ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        var response = await _booksApi.CreateAsync(new
        {
            ActorUsername = actorUsername,
            Isbn = normalizedIsbn,
            Title = normalizedTitle,
            PublisherName = publisherName,
            PublishedYear = publishedDate?.Year,
            ImageUrl = imageUrl,
            AuthorNames = parsedAuthors,
            CategoryNames = parsedCategories,
            InitialCopies = initialCopies
        }, cancellationToken);

        if (!response.Success)
        {
            return RedirectCreateWithError(UiTextLocalizer.TranslateMessage(response.Message), normalizedIsbn, normalizedTitle, publisherName, publishedDate, authorNames, categoryNames, initialCopies);
        }

        TempData["Success"] = UiTextLocalizer.TranslateMessage(response.Message);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int bookId, string isbn, string title, string? publisherName, DateTime? publishedDate, IFormFile? imageFile, string? authorNames, string? categoryNames, int desiredTotalCopies, CancellationToken cancellationToken)
    {
        var normalizedIsbn = isbn?.Trim() ?? string.Empty;
        var normalizedTitle = title?.Trim() ?? string.Empty;

        if (!IsManager())
        {
            TempData["Error"] = "Bạn không có quyền sửa sách.";
            return RedirectToAction(nameof(Index));
        }

        if (bookId <= 0)
        {
            TempData["Error"] = "Không tìm thấy sách cần sửa.";
            return RedirectToAction(nameof(Index));
        }

        if (string.IsNullOrWhiteSpace(normalizedIsbn) || string.IsNullOrWhiteSpace(normalizedTitle))
        {
            TempData["Error"] = "Vui lòng nhập ISBN và tên sách.";
            return RedirectToAction(nameof(Index));
        }

        if (!Regex.IsMatch(normalizedIsbn, "^\\d{13}$"))
        {
            TempData["Error"] = "ISBN phải gồm đúng 13 chữ số.";
            return RedirectToAction(nameof(Index));
        }

        if (desiredTotalCopies <= 0)
        {
            TempData["Error"] = "Tổng số lượng bản sao phải lớn hơn 0.";
            return RedirectToAction(nameof(Index));
        }

        if (publishedDate.HasValue && publishedDate.Value.Date > DateTime.Today)
        {
            TempData["Error"] = "Ngày xuất bản không được lớn hơn ngày hiện tại.";
            return RedirectToAction(nameof(Index));
        }

        var actorUsername = HttpContext.Session.GetString("Auth.Username") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(actorUsername))
        {
            return RedirectToAction("Login", "Auth");
        }

        string? imageUrl = null;
        if (imageFile is not null && imageFile.Length > 0)
        {
            var extension = Path.GetExtension(imageFile.FileName)?.ToLowerInvariant();
            var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".webp" };
            if (string.IsNullOrWhiteSpace(extension) || !allowedExtensions.Contains(extension))
            {
                TempData["Error"] = "Chỉ hỗ trợ ảnh định dạng PNG, JPG, JPEG hoặc WEBP.";
                return RedirectToAction(nameof(Index));
            }

            const long maxBytes = 5 * 1024 * 1024;
            if (imageFile.Length > maxBytes)
            {
                TempData["Error"] = "Kích thước ảnh tối đa là 5MB.";
                return RedirectToAction(nameof(Index));
            }

            var booksDirectory = Path.Combine(_environment.WebRootPath, "images", "books");
            Directory.CreateDirectory(booksDirectory);

            var fileName = $"book-{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(booksDirectory, fileName);

            await using (var stream = System.IO.File.Create(filePath))
            {
                await imageFile.CopyToAsync(stream, cancellationToken);
            }

            imageUrl = $"/images/books/{fileName}";
        }

        var parsedAuthors = (authorNames ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        var parsedCategories = (categoryNames ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        var response = await _booksApi.UpdateAsync(bookId, new
        {
            ActorUsername = actorUsername,
            BookId = bookId,
            Isbn = normalizedIsbn,
            Title = normalizedTitle,
            PublisherName = publisherName,
            PublishedYear = publishedDate?.Year,
            ImageUrl = imageUrl,
            AuthorNames = parsedAuthors,
            CategoryNames = parsedCategories,
            DesiredTotalCopies = desiredTotalCopies
        }, cancellationToken);

        TempData[response.Success ? "Success" : "Error"] = UiTextLocalizer.TranslateMessage(response.Message);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int bookId, CancellationToken cancellationToken)
    {
        if (!IsManager())
        {
            TempData["Error"] = "Bạn không có quyền ngừng bán sách.";
            return RedirectToAction(nameof(Index));
        }

        var actorUsername = HttpContext.Session.GetString("Auth.Username") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(actorUsername))
        {
            return RedirectToAction("Login", "Auth");
        }

        var response = await _booksApi.DeactivateAsync(bookId, cancellationToken);

        TempData[response.Success ? "Success" : "Error"] = UiTextLocalizer.TranslateMessage(response.Message);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateCopyFromDetails(int bookId, string barcode, string copyStatus, string? physicalCondition, string? locationCode, CancellationToken cancellationToken)
    {
        var actorUsername = HttpContext.Session.GetString("Auth.Username") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(actorUsername))
        {
            return RedirectToAction("Login", "Auth");
        }

        var response = await _bookCopiesApi.UpdateStatusAsync(barcode, new
        {
            ActorUsername = actorUsername,
            Barcode = barcode,
            CopyStatus = copyStatus,
            PhysicalCondition = physicalCondition,
            LocationCode = locationCode
        }, cancellationToken);

        TempData[response.Success ? "Success" : "Error"] = UiTextLocalizer.TranslateMessage(response.Message);
        return RedirectToAction(nameof(Details), new { id = bookId });
    }
}



