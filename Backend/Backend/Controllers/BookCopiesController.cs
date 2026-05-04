using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Server.Contracts.Books;
using Server.Contracts.Common;
using Server.Interface;
using Server.Services;

namespace Server.Controllers;

[ApiController]
[Route("api/book-copies")]
public sealed class BookCopiesController : ApiControllerBase
{
    private readonly ILibraryDataAccess _dataAccess;

    public BookCopiesController(ILibraryDataAccess dataAccess, CurrentUserService currentUser)
        : base(currentUser)
    {
        _dataAccess = dataAccess;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResult<List<object>>>> Lookup([FromQuery] string? query, CancellationToken cancellationToken)
    {
        var items = await _dataAccess.LookupBookCopiesAsync(query, cancellationToken);
        return Success(items);
    }

    [HttpPatch("{barcode}/status")]
    [Authorize(Policy = "ManagerOrStaff")]
    public async Task<ActionResult<ApiResult>> UpdateStatus(string barcode, [FromBody] UpdateBookCopyStatusRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await _dataAccess.UpdateBookCopyStatusAsync(barcode, request.CopyStatus, request.PhysicalCondition, request.LocationCode, cancellationToken);
            await AddActorSystemLogAsync(_dataAccess, "UPDATE", "BookCopy", barcode, $"Cập nhật bản sao {barcode}: {request.CopyStatus}", cancellationToken);
            return SuccessMessage("Đã cập nhật trạng thái bản sao.");
        }
        catch (InvalidOperationException ex)
        {
            return HandleInvalidOperation(ex);
        }
    }
}
