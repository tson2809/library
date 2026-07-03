using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Server.Contracts.Common;
using Server.Contracts.Manager;
using Server.Interface;
using Server.Services;

namespace Server.Controllers;

[ApiController]
[Route("api/fine-payments")]
[Authorize(Policy = "ManagerOrStaff")]
public sealed class FinePaymentsController : ApiControllerBase
{
    private readonly ILibraryDataAccess _dataAccess;

    public FinePaymentsController(ILibraryDataAccess dataAccess, CurrentUserService currentUser)
        : base(currentUser)
    {
        _dataAccess = dataAccess;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResult<object>>> GetHistory(
        [FromQuery] string? memberKeyword,
        [FromQuery] string? receivedByKeyword,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var exactReceivedByUsername = string.Equals(User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value, "staff", StringComparison.OrdinalIgnoreCase)
            ? CurrentUsername
            : null;

        var data = await _dataAccess.GetFinePaymentHistoryAsync(memberKeyword, receivedByKeyword, exactReceivedByUsername, fromDate, toDate, page, pageSize, cancellationToken);
        return Success(data);
    }

    [HttpPost]
    [Authorize(Policy = "StaffOnly")]
    public async Task<ActionResult<ApiResult<object>>> Collect([FromBody] CollectFinePaymentRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var paymentId = await _dataAccess.CollectFinePaymentAsync(request.MemberCode, request.LoanId, request.AmountPaid, request.PaymentMethod, request.Note, CurrentUsername, cancellationToken);
            await AddActorSystemLogAsync(_dataAccess, "COLLECT_FINE", "FinePayment", paymentId.ToString(), $"{CurrentUsername} thu tiền phạt: {request.AmountPaid:N0}", cancellationToken);
            return Created(string.Empty, ApiResult<object>.Ok(new { PaymentId = paymentId }, "Thu tiền phạt thành công."));
        }
        catch (InvalidOperationException ex)
        {
            return HandleInvalidOperation<object>(ex);
        }
    }
}
