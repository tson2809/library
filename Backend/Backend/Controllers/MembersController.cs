using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Server.Contracts.Common;
using Server.Contracts.Members;
using Server.Interface;
using Server.Services;

namespace Server.Controllers;

[ApiController]
[Route("api/members")]
public sealed class MembersController : ApiControllerBase
{
    private readonly ILibraryDataAccess _dataAccess;

    public MembersController(ILibraryDataAccess dataAccess, CurrentUserService currentUser)
        : base(currentUser)
    {
        _dataAccess = dataAccess;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResult<List<object>>>> GetMembers(CancellationToken cancellationToken)
    {
        var members = await _dataAccess.GetMembersListAsync(cancellationToken);
        return Success(members);
    }

    [HttpPost]
    [Authorize(Policy = "StaffOnly")]
    public async Task<ActionResult<ApiResult<object>>> CreateMember([FromBody] CreateMemberRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var memberId = await _dataAccess.CreateMemberAsync(request.FullName, request.Email, request.Phone, request.AddressLine, cancellationToken);
            await AddActorSystemLogAsync(_dataAccess, "CREATE", "Member", memberId.ToString(), $"Tạo thành viên: {request.FullName}", cancellationToken);
            return Created(string.Empty, ApiResult<object>.Ok(new { MemberId = memberId }, "Tạo thành viên thành công."));
        }
        catch (InvalidOperationException ex)
        {
            return HandleInvalidOperation<object>(ex);
        }
    }

    [HttpPut("{memberId:int}")]
    [Authorize(Policy = "StaffOnly")]
    public async Task<ActionResult<ApiResult>> UpdateMember(int memberId, [FromBody] UpdateMemberRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await _dataAccess.UpdateMemberAsync(memberId, request.Email, request.Phone, request.AddressLine, request.IsActive, cancellationToken);
            await AddActorSystemLogAsync(_dataAccess, "UPDATE", "Member", memberId.ToString(), $"Cập nhật thành viên #{memberId}", cancellationToken);
            return SuccessMessage("Cập nhật thành viên thành công.");
        }
        catch (InvalidOperationException ex)
        {
            return HandleInvalidOperation(ex);
        }
    }

    [HttpGet("{memberCode}/status")]
    [Authorize(Policy = "StaffOnly")]
    public async Task<ActionResult<ApiResult<object>>> GetMemberStatus(string memberCode, CancellationToken cancellationToken)
    {
        var status = await _dataAccess.GetMemberBorrowingStatusAsync(memberCode, cancellationToken);
        return status is null
            ? Failure<object>("Không tìm thấy thành viên.", StatusCodes.Status404NotFound)
            : Success(status);
    }

    [HttpPost("verify-access")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResult<MemberPortalSummaryDto>>> VerifyAccess([FromBody] VerifyMemberAccessRequest request, CancellationToken cancellationToken)
    {
        var member = await _dataAccess.VerifyMemberAccessAsync(request.MemberCode, request.PhoneOrEmail, cancellationToken);
        return member is null
            ? Failure<MemberPortalSummaryDto>("Thông tin thành viên không hợp lệ hoặc tài khoản đã ngừng hoạt động.", StatusCodes.Status401Unauthorized)
            : Success(member, "Xác thực thành viên thành công.");
    }

    [HttpGet("{memberCode}/statement")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResult<MemberStatementDto>>> GetMemberStatement(string memberCode, CancellationToken cancellationToken)
    {
        var statement = await _dataAccess.GetMemberStatementAsync(memberCode, cancellationToken);
        return statement is null
            ? Failure<MemberStatementDto>("Không tìm thấy thành viên.", StatusCodes.Status404NotFound)
            : Success(statement);
    }
}
