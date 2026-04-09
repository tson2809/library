using System.Text.Json;
using Client_web.Models.ViewModels;
using Client_web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Client_web.Controllers;

public sealed class CategoriesController : Controller
{
    private readonly ICategoriesApiClient _categoriesApi;
    private const string CategoryListViewName = "CategoryList";

    public CategoriesController(ICategoriesApiClient categoriesApi)
    {
        _categoriesApi = categoriesApi;
    }

    private bool IsManager()
    {
        var role = HttpContext.Session.GetString("Auth.Role");
        return string.Equals(role, "Quản lý", StringComparison.OrdinalIgnoreCase);
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (!IsManager())
        {
            TempData["Error"] = "Bạn không có quyền truy cập chức năng thể loại.";
            return RedirectToAction("Index", "Home");
        }

        var response = await _categoriesApi.GetCategoriesAsync(cancellationToken);
        if (!response.Success)
        {
            ViewBag.Error = UiTextLocalizer.TranslateMessage(response.Message);
            return View(CategoryListViewName, new List<CategoryVm>());
        }

        var categories = response.Data.Deserialize<List<CategoryVm>>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new List<CategoryVm>();

        return View(CategoryListViewName, categories);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string categoryName, string? description, CancellationToken cancellationToken)
    {
        if (!IsManager())
        {
            TempData["Error"] = "Bạn không có quyền tạo thể loại.";
            return RedirectToAction("Index", "Home");
        }

        if (string.IsNullOrWhiteSpace(categoryName))
        {
            TempData["CreateCategoryError"] = "Tên thể loại không được để trống.";
            TempData["CreateCategoryName"] = categoryName ?? string.Empty;
            TempData["CreateCategoryDescription"] = description ?? string.Empty;
            return RedirectToAction(nameof(Index));
        }

        var actorUsername = HttpContext.Session.GetString("Auth.Username") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(actorUsername))
        {
            return RedirectToAction("Login", "Auth");
        }

        var response = await _categoriesApi.CreateAsync(new
        {
            ActorUsername = actorUsername,
            CategoryName = categoryName,
            Description = description
        }, cancellationToken);

        if (!response.Success)
        {
            TempData["CreateCategoryError"] = UiTextLocalizer.TranslateMessage(response.Message);
            TempData["CreateCategoryName"] = categoryName ?? string.Empty;
            TempData["CreateCategoryDescription"] = description ?? string.Empty;
            return RedirectToAction(nameof(Index));
        }

        TempData["Success"] = UiTextLocalizer.TranslateMessage(response.Message);
        return RedirectToAction(nameof(Index));
    }
}



