using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Server.Contracts.Books;
using Server.Contracts.Common;
using Server.Interface;
using Server.Services;

namespace Server.Controllers;

[ApiController]
[Route("api/books")]
public sealed class BooksController : ApiControllerBase
{
    private readonly ILibraryDataAccess _dataAccess;
    private readonly IsbnLookupService _isbnLookupService;

    public BooksController(ILibraryDataAccess dataAccess, IsbnLookupService isbnLookupService, CurrentUserService currentUser)
        : base(currentUser)
    {
        _dataAccess = dataAccess;
        _isbnLookupService = isbnLookupService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResult<List<object>>>> GetBooks(CancellationToken cancellationToken)
    {
        var books = await _dataAccess.GetBooksListAsync(cancellationToken);
        return Success(books);
    }

    [HttpGet("{bookId:int}")]
    public async Task<ActionResult<ApiResult<object>>> GetBook(int bookId, CancellationToken cancellationToken)
    {
        var book = await _dataAccess.GetBookDetailAsync(bookId, cancellationToken);
        return book is null
            ? Failure<object>("Không tìm thấy sách.", StatusCodes.Status404NotFound)
            : Success(book);
    }

    [HttpGet("max-barcode")]
    [Authorize(Policy = "ManagerOnly")]
    public async Task<ActionResult<ApiResult<MaxBarcodeDto>>> GetMaxBarcode(CancellationToken cancellationToken)
    {
        var maxBarcode = await _dataAccess.GetMaxBarcodeAsync(cancellationToken);
        return Success(new MaxBarcodeDto { MaxBarcode = maxBarcode });
    }

    [HttpGet("isbn-lookup")]
    [Authorize(Policy = "ManagerOnly")]
    public async Task<ActionResult<ApiResult<object>>> LookupIsbn([FromQuery] string isbn, CancellationToken cancellationToken)
    {
        var digits = new string((isbn ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.Length is not 10 and not 13)
        {
            return Failure<object>("ISBN phải gồm 10 hoặc 13 chữ số.");
        }

        var result = await _isbnLookupService.LookupAsync(digits, cancellationToken);
        return result is null
            ? Failure<object>("Không tìm thấy thông tin sách cho ISBN này. Vui lòng nhập thủ công.", StatusCodes.Status404NotFound)
            : Success<object>(result, "Tra cứu ISBN thành công.");
    }

    [HttpPost]
    [Authorize(Policy = "ManagerOnly")]
    public async Task<ActionResult<ApiResult<object>>> CreateBook([FromBody] CreateBookRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var bookId = await _dataAccess.CreateBookAsync(CurrentUsername, request.Isbn, request.Title, request.PublisherName, request.PublishedYear, request.ImageUrl, request.AuthorNames, request.CategoryNames, request.InitialCopies, cancellationToken);
            await AddActorSystemLogAsync(_dataAccess, "CREATE", "Book", bookId.ToString(), $"Tạo sách: {request.Title} (ISBN: {request.Isbn})", cancellationToken);
            return CreatedAtAction(nameof(GetBook), new { bookId }, ApiResult<object>.Ok(new { BookId = bookId }, "Thêm sách thành công."));
        }
        catch (InvalidOperationException ex)
        {
            return HandleInvalidOperation<object>(ex);
        }
    }

    [HttpPut("{bookId:int}")]
    [Authorize(Policy = "ManagerOnly")]
    public async Task<ActionResult<ApiResult>> UpdateBook(int bookId, [FromBody] UpdateBookRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await _dataAccess.UpdateBookAsync(CurrentUsername, bookId, request.Isbn, request.Title, request.PublisherName, request.PublishedYear, request.ImageUrl, request.AuthorNames, request.CategoryNames, request.DesiredTotalCopies, cancellationToken);
            await AddActorSystemLogAsync(_dataAccess, "UPDATE", "Book", bookId.ToString(), $"Cập nhật sách: {request.Title}", cancellationToken);
            return SuccessMessage("Cập nhật sách thành công.");
        }
        catch (InvalidOperationException ex)
        {
            return HandleInvalidOperation(ex);
        }
    }

    [HttpPost("{bookId:int}/deactivate")]
    [Authorize(Policy = "ManagerOnly")]
    public async Task<ActionResult<ApiResult>> DeactivateBook(int bookId, CancellationToken cancellationToken)
    {
        try
        {
            await _dataAccess.DeactivateBookAsync(CurrentUsername, bookId, cancellationToken);
            await AddActorSystemLogAsync(_dataAccess, "DEACTIVATE", "Book", bookId.ToString(), $"Ngừng bán sách #{bookId}", cancellationToken);
            return SuccessMessage("Đã ngừng bán sách thành công.");
        }
        catch (InvalidOperationException ex)
        {
            return HandleInvalidOperation(ex);
        }
    }
}
