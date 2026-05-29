using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Server.Contracts.Common;
using Server.Contracts.Loans;
using Server.Interface;
using Server.Services;

namespace Server.Controllers;

[ApiController]
[Route("api/loans")]
[Authorize(Policy = "ManagerOrStaff")]
public sealed class LoansController : ApiControllerBase
{
    private readonly ILibraryDataAccess _dataAccess;

    public LoansController(ILibraryDataAccess dataAccess, CurrentUserService currentUser)
        : base(currentUser)
    {
        _dataAccess = dataAccess;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResult<List<object>>>> GetLoans(CancellationToken cancellationToken)
    {
        var loans = await _dataAccess.GetLoansListAsync(cancellationToken);
        return Success(loans);
    }

    [HttpGet("{loanId:int}")]
    [Authorize(Policy = "StaffOnly")]
    public async Task<ActionResult<ApiResult<object>>> GetLoan(int loanId, CancellationToken cancellationToken)
    {
        var detail = await _dataAccess.GetLoanDetailAsync(loanId, cancellationToken);
        return detail is null
            ? Failure<object>("Không tìm thấy phiếu mượn.", StatusCodes.Status404NotFound)
            : Success(detail);
    }

    [HttpPost]
    [Authorize(Policy = "StaffOnly")]
    public async Task<ActionResult<ApiResult<CreateLoanResultDto>>> CreateLoan([FromBody] CreateLoanRequest request, CancellationToken cancellationToken)
    {
        if (request.DueDate <= DateOnly.FromDateTime(DateTime.Today))
        {
            return Failure<CreateLoanResultDto>("Ngày trả phải sau ngày hiện tại.");
        }

        try
        {
            var loanId = await _dataAccess.CreateLoanAsync(request.MemberCode, CurrentUsername, request.DueDate, request.Barcodes, request.ConditionBefore, request.Note, cancellationToken);
            await AddActorSystemLogAsync(_dataAccess, "LOAN_CREATE", "Loan", loanId.ToString(), $"{CurrentUsername} tạo phiếu mượn cho {request.MemberCode}, {request.Barcodes.Count} cuốn", cancellationToken);
            return CreatedAtAction(nameof(GetLoan), new { loanId }, ApiResult<CreateLoanResultDto>.Ok(new CreateLoanResultDto { LoanId = loanId }, "Tạo phiếu mượn thành công."));
        }
        catch (InvalidOperationException ex)
        {
            return HandleInvalidOperation<CreateLoanResultDto>(ex);
        }
        catch (DbUpdateException ex)
        {
            var detail = ex.InnerException?.Message;
            return Failure<CreateLoanResultDto>(string.IsNullOrWhiteSpace(detail)
                ? "Không thể lưu phiếu mượn vào cơ sở dữ liệu."
                : $"Không thể lưu phiếu mượn: {detail}", StatusCodes.Status500InternalServerError);
        }
    }

    [HttpPost("return-by-barcode")]
    [Authorize(Policy = "StaffOnly")]
    public async Task<ActionResult<ApiResult<ReturnByBarcodeResultDto>>> ReturnByBarcode([FromBody] ReturnByBarcodeRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _dataAccess.ReturnBookByBarcodeAsync(request.Barcode, request.ConditionAfter, cancellationToken);
            await AddActorSystemLogAsync(_dataAccess, "LOAN_RETURN", "Loan", result.LoanId.ToString(), $"{CurrentUsername} nhận trả sách mã vạch {request.Barcode} ({request.ConditionAfter ?? "n/a"})", cancellationToken);

            var message = string.IsNullOrWhiteSpace(result.ReservedForMemberCode)
                ? "Trả sách thành công."
                : $"Trả sách thành công. Sách đã được giữ ưu tiên cho {result.ReservedForMemberName} ({result.ReservedForMemberCode}).";

            return Success(result, message);
        }
        catch (InvalidOperationException ex)
        {
            return HandleInvalidOperation<ReturnByBarcodeResultDto>(ex);
        }
    }

    [HttpPost("{loanId:int}/renew")]
    [Authorize(Policy = "StaffOnly")]
    public async Task<ActionResult<ApiResult<RenewLoanResultDto>>> RenewLoan(int loanId, [FromBody] RenewLoanRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _dataAccess.RenewLoanWithResultAsync(loanId, request.NewDueDate, cancellationToken);
            await AddActorSystemLogAsync(_dataAccess, "LOAN_RENEW", "Loan", loanId.ToString(), $"{CurrentUsername} gia hạn phiếu #{loanId} đến {request.NewDueDate}", cancellationToken);
            return Success(result, "Gia hạn phiếu mượn thành công.");
        }
        catch (InvalidOperationException ex)
        {
            return HandleInvalidOperation<RenewLoanResultDto>(ex);
        }
    }
}
