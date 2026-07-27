using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Server.Contracts.Common;
using Server.Interface;
using Server.Services;

namespace Server.Controllers;

[ApiController]
[Route("api/system-logs")]
[Authorize(Policy = "ManagerOnly")]
public sealed class SystemLogsController : ApiControllerBase
{
    private readonly ILibraryDataAccess _dataAccess;

    public SystemLogsController(ILibraryDataAccess dataAccess, CurrentUserService currentUser)
        : base(currentUser)
    {
        _dataAccess = dataAccess;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResult<List<object>>>> GetLogs(CancellationToken cancellationToken)
    {
        var logs = await _dataAccess.GetSystemLogsAsync(cancellationToken);
        return Success(logs);
    }
}
