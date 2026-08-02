using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Server.Contracts.Common;
using Server.Contracts.Notifications;
using Server.Interface;
using Server.Services;

namespace Server.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public sealed class NotificationsController : ApiControllerBase
{
    private readonly ILibraryDataAccess _dataAccess;

    public NotificationsController(ILibraryDataAccess dataAccess, CurrentUserService currentUser)
        : base(currentUser)
    {
        _dataAccess = dataAccess;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResult<List<object>>>> GetNotifications(CancellationToken cancellationToken)
    {
        var notifications = await _dataAccess.GetNotificationsForUserAsync(CurrentUsername, cancellationToken);
        return Success(notifications);
    }

    [HttpGet("{notificationId:int}")]
    public async Task<ActionResult<ApiResult<object>>> GetNotification(int notificationId, CancellationToken cancellationToken)
    {
        var item = await _dataAccess.GetNotificationDetailForUserAsync(CurrentUsername, notificationId, cancellationToken);
        return item is null
            ? Failure<object>("Không tìm thấy thông báo hoặc bạn không có quyền xem.", StatusCodes.Status404NotFound)
            : Success(item);
    }

    [HttpPost]
    [Authorize(Policy = "ManagerOnly")]
    public async Task<ActionResult<ApiResult<object>>> CreateNotification([FromBody] CreateNotificationRequest request, CancellationToken cancellationToken)
    {
        if (!request.SendToAll && request.RecipientUsernames.Count == 0)
        {
            return Failure<object>("Vui lòng chọn ít nhất một người nhận hoặc bật gửi toàn bộ.");
        }

        try
        {
            var notificationId = await _dataAccess.CreateNotificationAsync(CurrentUsername, request.Title, request.Content, request.SendToAll, request.RecipientUsernames, cancellationToken);
            await AddActorSystemLogAsync(_dataAccess, "NOTIFICATION_CREATE", "Notification", notificationId.ToString(), $"{CurrentUsername} tạo thông báo: {request.Title}", cancellationToken);
            return CreatedAtAction(nameof(GetNotification), new { notificationId }, ApiResult<object>.Ok(new { NotificationId = notificationId }, "Tạo thông báo thành công."));
        }
        catch (InvalidOperationException ex)
        {
            return HandleInvalidOperation<object>(ex);
        }
    }
}
