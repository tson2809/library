using Microsoft.AspNetCore.Mvc;
using Server.Contracts.Common;
using Server.Interface;
using Server.Services;

namespace Server.Controllers;

public abstract class ApiControllerBase : ControllerBase
{
    private readonly CurrentUserService _currentUser;

    protected ApiControllerBase(CurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    protected string CurrentUsername => _currentUser.Username ?? string.Empty;

    protected int? CurrentUserId => _currentUser.UserId;

    protected ActionResult<ApiResult<T>> Success<T>(T? data, string message = "Thành công.")
    {
        return Ok(ApiResult<T>.Ok(data, message));
    }

    protected ActionResult<ApiResult> SuccessMessage(string message = "Thành công.")
    {
        return Ok(ApiResult.Ok(message));
    }

    protected ActionResult<ApiResult<T>> Failure<T>(string message, int statusCode = StatusCodes.Status400BadRequest)
    {
        return StatusCode(statusCode, ApiResult<T>.Fail(message));
    }

    protected ActionResult<ApiResult> Failure(string message, int statusCode = StatusCodes.Status400BadRequest)
    {
        return StatusCode(statusCode, ApiResult.Fail(message));
    }

    protected ActionResult<ApiResult<T>> HandleInvalidOperation<T>(InvalidOperationException ex)
    {
        return Failure<T>(ex.Message, StatusCodes.Status400BadRequest);
    }

    protected ActionResult<ApiResult> HandleInvalidOperation(InvalidOperationException ex)
    {
        return Failure(ex.Message, StatusCodes.Status400BadRequest);
    }

    protected async Task AddActorSystemLogAsync(
        ILibraryDataAccess dataAccess,
        string actionType,
        string entityName,
        string? entityId,
        string? description,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = CurrentUserId;
            if (!userId.HasValue && !string.IsNullOrWhiteSpace(CurrentUsername))
            {
                userId = await dataAccess.GetUserIdByUsernameAsync(CurrentUsername, cancellationToken);
            }

            await dataAccess.AddSystemLogAsync(actionType, entityName, entityId, description, userId, cancellationToken);
        }
        catch
        {
        }
    }
}
