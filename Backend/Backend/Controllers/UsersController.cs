using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Server.Contracts.Common;
using Server.Contracts.Users;
using Server.Interface;
using Server.Services;

namespace Server.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Policy = "ManagerOnly")]
public sealed class UsersController : ApiControllerBase
{
    private readonly ILibraryDataAccess _dataAccess;
    private readonly PasswordHashService _passwordHashService;

    public UsersController(ILibraryDataAccess dataAccess, PasswordHashService passwordHashService, CurrentUserService currentUser)
        : base(currentUser)
    {
        _dataAccess = dataAccess;
        _passwordHashService = passwordHashService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResult<List<object>>>> GetUsers(CancellationToken cancellationToken)
    {
        try
        {
            var users = await _dataAccess.GetUsersListAsync(CurrentUsername, cancellationToken);
            return Success(users);
        }
        catch (InvalidOperationException ex)
        {
            return HandleInvalidOperation<List<object>>(ex);
        }
    }

    [HttpPost]
    public async Task<ActionResult<ApiResult<object>>> CreateUser([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = await _dataAccess.CreateUserAsync(CurrentUsername, request.Username, request.FullName, request.Email, request.Phone, request.RoleName, _passwordHashService.HashPassword(request.Password), cancellationToken);
            await AddActorSystemLogAsync(_dataAccess, "CREATE", "User", userId.ToString(), $"Tạo tài khoản: {request.Username}", cancellationToken);
            return Created(string.Empty, ApiResult<object>.Ok(new { UserId = userId }, "Tạo tài khoản thành công."));
        }
        catch (InvalidOperationException ex)
        {
            return HandleInvalidOperation<object>(ex);
        }
    }

    [HttpPut("{userId:int}")]
    public async Task<ActionResult<ApiResult>> UpdateUser(int userId, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await _dataAccess.UpdateUserAsync(CurrentUsername, userId, request.FullName, request.Email, request.Phone, request.RoleName, request.IsActive, cancellationToken);
            await AddActorSystemLogAsync(_dataAccess, "UPDATE", "User", userId.ToString(), $"Cập nhật tài khoản #{userId}", cancellationToken);
            return SuccessMessage("Cập nhật tài khoản thành công.");
        }
        catch (InvalidOperationException ex)
        {
            return HandleInvalidOperation(ex);
        }
    }

    [HttpPost("{userId:int}/reset-password")]
    public async Task<ActionResult<ApiResult>> ResetPassword(int userId, [FromBody] ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await _dataAccess.ResetUserPasswordAsync(CurrentUsername, userId, _passwordHashService.HashPassword(request.NewPassword), cancellationToken);
            await AddActorSystemLogAsync(_dataAccess, "RESET_PASSWORD", "User", userId.ToString(), $"Đặt lại mật khẩu user #{userId}", cancellationToken);
            return SuccessMessage("Đặt lại mật khẩu thành công.");
        }
        catch (InvalidOperationException ex)
        {
            return HandleInvalidOperation(ex);
        }
    }
}
