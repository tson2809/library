using System.Text.Json;
using Client_web.Models.ViewModels;
using Client_web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Client_web.Controllers;

public sealed class MembersController : Controller
{
    private readonly IMembersApiClient _membersApi;
    private const string MemberListViewName = "MemberList";

    public MembersController(IMembersApiClient membersApi)
    {
        _membersApi = membersApi;
    }

    private bool IsManager()
    {
        var role = HttpContext.Session.GetString("Auth.Role");
        return string.Equals(role, "Quản lý", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var response = await _membersApi.GetMembersAsync(cancellationToken);
        if (!response.Success)
        {
            ViewBag.Error = UiTextLocalizer.TranslateMessage(response.Message);
            return View(MemberListViewName, new List<MemberVm>());
        }

        var items = response.Data.Deserialize<List<MemberVm>>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new List<MemberVm>();

        return View(MemberListViewName, items);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string fullName, string? email, string? phone, CancellationToken cancellationToken)
    {
        if (IsManager())
        {
            TempData["Error"] = "Quản lý chỉ được xem danh sách thành viên.";
            return RedirectToAction(nameof(Index));
        }

        var actorUsername = HttpContext.Session.GetString("Auth.Username") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(actorUsername))
        {
            return RedirectToAction("Login", "Auth");
        }

        var response = await _membersApi.CreateAsync(new
        {
            ActorUsername = actorUsername,
            FullName = fullName,
            Email = email,
            Phone = phone
        }, cancellationToken);

        if (response.Success)
        {
            TempData["Success"] = UiTextLocalizer.TranslateMessage(response.Message);
        }
        else
        {
            TempData["CreateMemberError"] = UiTextLocalizer.TranslateMessage(response.Message);
            TempData["CreateMemberFullName"] = fullName ?? string.Empty;
            TempData["CreateMemberEmail"] = email ?? string.Empty;
            TempData["CreateMemberPhone"] = phone ?? string.Empty;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int memberId, string? email, string? phone, bool isActive, CancellationToken cancellationToken)
    {
        if (IsManager())
        {
            TempData["Error"] = "Quản lý chỉ được xem danh sách thành viên.";
            return RedirectToAction(nameof(Index));
        }

        var actorUsername = HttpContext.Session.GetString("Auth.Username") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(actorUsername))
        {
            return RedirectToAction("Login", "Auth");
        }

        var response = await _membersApi.UpdateAsync(memberId, new
        {
            ActorUsername = actorUsername,
            MemberId = memberId,
            Email = email,
            Phone = phone,
            IsActive = isActive
        }, cancellationToken);

        TempData[response.Success ? "Success" : "Error"] = UiTextLocalizer.TranslateMessage(response.Message);
        return RedirectToAction(nameof(Index));
    }
}



