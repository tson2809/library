using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Server.Contracts.Common;
using Server.Contracts.Loans;
using Server.Interface;
using Server.Services;

namespace Server.Controllers;

[ApiController]
[Route("api/reservations")]
[Authorize(Policy = "StaffOnly")]
public sealed class ReservationsController : ApiControllerBase
{
    private readonly ILibraryDataAccess _dataAccess;

    public ReservationsController(ILibraryDataAccess dataAccess, CurrentUserService currentUser)
        : base(currentUser)
    {
        _dataAccess = dataAccess;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResult<List<ReservationDto>>>> GetReservations([FromQuery] string? status, CancellationToken cancellationToken)
    {
        var reservations = await _dataAccess.GetReservationsAsync(status, cancellationToken);
        return Success(reservations);
    }

    [HttpPost]
    public async Task<ActionResult<ApiResult<object>>> CreateReservation([FromBody] CreateReservationRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var reservationId = await _dataAccess.CreateReservationAsync(request.MemberCode, request.BookId, CurrentUsername, request.Note, cancellationToken);
            await AddActorSystemLogAsync(_dataAccess, "RESERVATION_CREATE", "BookReservation", reservationId.ToString(), $"{CurrentUsername} tạo đặt trước sách #{request.BookId} cho {request.MemberCode}", cancellationToken);
            return Created(string.Empty, ApiResult<object>.Ok(new { ReservationId = reservationId }, "Tạo yêu cầu đặt trước thành công."));
        }
        catch (InvalidOperationException ex)
        {
            return HandleInvalidOperation<object>(ex);
        }
    }

    [HttpPost("{reservationId:int}/cancel")]
    public async Task<ActionResult<ApiResult<ReservationActionResultDto>>> CancelReservation(int reservationId, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _dataAccess.CancelReservationAsync(reservationId, CurrentUsername, cancellationToken);
            await AddActorSystemLogAsync(_dataAccess, "RESERVATION_CANCEL", "BookReservation", reservationId.ToString(), $"{CurrentUsername} hủy đặt trước #{reservationId}", cancellationToken);

            var message = string.IsNullOrWhiteSpace(result.ReassignedMemberCode)
                ? "Đã hủy yêu cầu đặt trước."
                : $"Đã hủy yêu cầu đặt trước. Bản sao {result.ReassignedBarcode} đã được chuyển giữ cho {result.ReassignedMemberCode}.";

            return Success(result, message);
        }
        catch (InvalidOperationException ex)
        {
            return HandleInvalidOperation<ReservationActionResultDto>(ex);
        }
    }
}
