using System.Text.Json;
using Client_web.Models.ViewModels;
using Client_web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Client_web.Controllers;

public sealed class NotificationsController : Controller
{
    private readonly INotificationsApiClient _notificationsApi;
    private readonly IUsersApiClient _usersApi;

    public NotificationsController(INotificationsApiClient notificationsApi, IUsersApiClient usersApi)
    {
        _notificationsApi = notificationsApi;
        _usersApi = usersApi;
    }

    private string? CurrentUsername() => HttpContext.Session.GetString("Auth.Username");

    private bool IsManager()
    {
        var role = HttpContext.Session.GetString("Auth.Role");
        return string.Equals(role, "Quản lý", StringComparison.OrdinalIgnoreCase);
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var actorUsername = CurrentUsername();
        if (string.IsNullOrWhiteSpace(actorUsername))
        {
            return RedirectToAction("Login", "Auth");
        }

        var response = await _notificationsApi.GetNotificationsAsync(cancellationToken);

        if (!response.Success)
        {
            TempData["Error"] = UiTextLocalizer.TranslateMessage(response.Message);
            return View(new List<NotificationListItemVm>());
        }

        var items = response.Data.Deserialize<List<NotificationListItemVm>>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new List<NotificationListItemVm>();

        List<ManagerUserVm> recipients = new();
        if (IsManager())
        {
            var usersResponse = await _usersApi.GetUsersAsync(cancellationToken);

            if (usersResponse.Success)
            {
                recipients = usersResponse.Data.Deserialize<List<ManagerUserVm>>(new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new List<ManagerUserVm>();
            }
        }

        ViewBag.IsManager = IsManager();
        ViewBag.Recipients = recipients.Where(u => u.IsActive).OrderBy(u => u.FullName).ToList();
        return View(items);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(NotificationComposeVm model, CancellationToken cancellationToken)
    {
        var actorUsername = CurrentUsername();
        if (string.IsNullOrWhiteSpace(actorUsername))
        {
            return RedirectToAction("Login", "Auth");
        }

        if (!IsManager())
        {
            return RedirectToAction(nameof(Index));
        }

        var response = await _notificationsApi.CreateAsync(new
        {
            ActorUsername = actorUsername,
            model.Title,
            model.Content,
            model.SendToAll,
            RecipientUsernames = model.RecipientUsernames
        }, cancellationToken);

        TempData[response.Success ? "Success" : "Error"] = UiTextLocalizer.TranslateMessage(response.Message);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Detail(int id, CancellationToken cancellationToken)
    {
        var actorUsername = CurrentUsername();
        if (string.IsNullOrWhiteSpace(actorUsername))
        {
            return RedirectToAction("Login", "Auth");
        }

        var response = await _notificationsApi.GetDetailAsync(id, cancellationToken);

        if (!response.Success)
        {
            TempData["Error"] = UiTextLocalizer.TranslateMessage(response.Message);
            return RedirectToAction(nameof(Index));
        }

        var item = response.Data.Deserialize<NotificationDetailVm>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (item is null)
        {
            TempData["Error"] = "Không đọc được nội dung thông báo.";
            return RedirectToAction(nameof(Index));
        }

        return View(item);
    }
}
