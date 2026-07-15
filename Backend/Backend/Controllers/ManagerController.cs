using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Server.Contracts.Common;
using Server.Interface;
using Server.Services;

namespace Server.Controllers;

[ApiController]
[Route("api/manager")]
[Authorize(Policy = "ManagerOnly")]
public sealed class ManagerController : ApiControllerBase
{
    private readonly ILibraryDataAccess _dataAccess;

    public ManagerController(ILibraryDataAccess dataAccess, CurrentUserService currentUser)
        : base(currentUser)
    {
        _dataAccess = dataAccess;
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<ApiResult<object>>> Dashboard(CancellationToken cancellationToken)
    {
        var dashboard = await _dataAccess.GetManagerDashboardAsync(cancellationToken);
        return Success(dashboard);
    }

    [HttpGet("revenue")]
    public async Task<ActionResult<ApiResult<object>>> Revenue([FromQuery] int? year, CancellationToken cancellationToken)
    {
        var data = await _dataAccess.GetManagerRevenueAsync(year ?? DateTime.Today.Year, cancellationToken);
        return Success(data);
    }
}
