using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Server.Contracts.Common;
using Server.Interface;
using Server.Services;

namespace Server.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Policy = "ManagerOnly")]
public sealed class ReportsController : ApiControllerBase
{
    private readonly ILibraryDataAccess _dataAccess;

    public ReportsController(ILibraryDataAccess dataAccess, CurrentUserService currentUser)
        : base(currentUser)
    {
        _dataAccess = dataAccess;
    }

    [HttpGet("overdue-loans")]
    public async Task<ActionResult<ApiResult<List<object>>>> OverdueLoans(CancellationToken cancellationToken)
    {
        var report = await _dataAccess.GetOverdueReportAsync(cancellationToken);
        return Success(report);
    }
}
