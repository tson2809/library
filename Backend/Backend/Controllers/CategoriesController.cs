using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Server.Contracts.Categories;
using Server.Contracts.Common;
using Server.Interface;
using Server.Services;

namespace Server.Controllers;

[ApiController]
[Route("api/categories")]
public sealed class CategoriesController : ApiControllerBase
{
    private readonly ILibraryDataAccess _dataAccess;

    public CategoriesController(ILibraryDataAccess dataAccess, CurrentUserService currentUser)
        : base(currentUser)
    {
        _dataAccess = dataAccess;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResult<List<object>>>> GetCategories(CancellationToken cancellationToken)
    {
        var categories = await _dataAccess.GetCategoriesListAsync(cancellationToken);
        return Success(categories);
    }

    [HttpPost]
    [Authorize(Policy = "ManagerOnly")]
    public async Task<ActionResult<ApiResult<object>>> CreateCategory([FromBody] CreateCategoryRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var categoryId = await _dataAccess.CreateCategoryAsync(CurrentUsername, request.CategoryName, request.Description, cancellationToken);
            await AddActorSystemLogAsync(_dataAccess, "CREATE", "Category", categoryId.ToString(), $"Tạo thể loại: {request.CategoryName}", cancellationToken);
            return Created(string.Empty, ApiResult<object>.Ok(new { CategoryId = categoryId }, "Tạo thể loại thành công."));
        }
        catch (InvalidOperationException ex)
        {
            return HandleInvalidOperation<object>(ex);
        }
    }
}
