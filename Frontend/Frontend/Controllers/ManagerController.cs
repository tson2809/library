using System.Text.Json;
using Client_web.Models.ViewModels;
using Client_web.Services;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;

namespace Client_web.Controllers;

public sealed class ManagerController : Controller
{
    private readonly IUsersApiClient _usersApi;
    private readonly IFinePaymentsApiClient _finePaymentsApi;
    private const string EmployeeListViewName = "EmployeeList";

    public ManagerController(IUsersApiClient usersApi, IFinePaymentsApiClient finePaymentsApi)
    {
        _usersApi = usersApi;
        _finePaymentsApi = finePaymentsApi;
    }

    private bool IsManager()
    {
        var role = HttpContext.Session.GetString("Auth.Role");
        return string.Equals(role, "Quản lý", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsStaff()
    {
        var role = HttpContext.Session.GetString("Auth.Role");
        return string.Equals(role, "Nhân viên", StringComparison.OrdinalIgnoreCase);
    }

    private IActionResult? RedirectIfNotManager()
    {
        if (IsManager())
        {
            return null;
        }

        return RedirectToAction("Index", "Home");
    }

    private IActionResult? RedirectIfNoFinePaymentAccess()
    {
        if (IsManager() || IsStaff())
        {
            return null;
        }

        return RedirectToAction("Index", "Home");
    }

    private string? CurrentUsername()
    {
        return HttpContext.Session.GetString("Auth.Username");
    }

    private IActionResult RedirectCreateWithError(string message, string username, string fullName, string? email, string? phone, string roleName)
    {
        TempData["CreateEmployeeError"] = message;
        TempData["CreateEmployeeUsername"] = username ?? string.Empty;
        TempData["CreateEmployeeFullName"] = fullName ?? string.Empty;
        TempData["CreateEmployeeEmail"] = email ?? string.Empty;
        TempData["CreateEmployeePhone"] = phone ?? string.Empty;
        TempData["CreateEmployeeRole"] = roleName ?? "staff";
        return RedirectToAction(nameof(Index));
    }

    private IActionResult RedirectEditWithError(string message, int userId, string fullName, string? email, string? phone, string roleName, bool isActive)
    {
        TempData["EditEmployeeError"] = message;
        TempData["EditEmployeeUserId"] = userId;
        TempData["EditEmployeeFullName"] = fullName ?? string.Empty;
        TempData["EditEmployeeEmail"] = email ?? string.Empty;
        TempData["EditEmployeePhone"] = phone ?? string.Empty;
        TempData["EditEmployeeRole"] = roleName ?? "staff";
        TempData["EditEmployeeActive"] = isActive;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var denyResult = RedirectIfNotManager();
        if (denyResult is not null)
        {
            return denyResult;
        }

        var actorUsername = CurrentUsername();
        if (string.IsNullOrWhiteSpace(actorUsername))
        {
            return RedirectToAction("Login", "Auth");
        }

        var response = await _usersApi.GetUsersAsync(cancellationToken);
        if (!response.Success)
        {
            ViewBag.Error = UiTextLocalizer.TranslateMessage(response.Message);
            return View(EmployeeListViewName, new List<ManagerUserVm>());
        }

        var users = response.Data.Deserialize<List<ManagerUserVm>>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new List<ManagerUserVm>();

        return View(EmployeeListViewName, users);
    }

    [HttpGet]
    public async Task<IActionResult> FinePaymentHistory(string? memberKeyword, string? receivedByKeyword, string? fromDate, string? toDate, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var denyResult = RedirectIfNoFinePaymentAccess();
        if (denyResult is not null)
        {
            return denyResult;
        }

        var actorUsername = CurrentUsername();
        if (string.IsNullOrWhiteSpace(actorUsername))
        {
            return RedirectToAction("Login", "Auth");
        }

        var isManager = IsManager();
        var isStaff = IsStaff();
        var safePageSize = pageSize is 5 or 10 or 20 ? pageSize : 10;
        var effectiveReceivedByKeyword = isStaff ? actorUsername : receivedByKeyword;

        ViewBag.MemberKeyword = memberKeyword ?? string.Empty;
        ViewBag.ReceivedByKeyword = effectiveReceivedByKeyword ?? string.Empty;
        ViewBag.FromDate = fromDate ?? string.Empty;
        ViewBag.ToDate = toDate ?? string.Empty;
        ViewBag.IsManager = isManager;
        ViewBag.IsStaff = isStaff;
        ViewBag.CurrentUsername = actorUsername;

        var rows = new List<FinePaymentHistoryVm>();
        var fetchPage = 1;
        const int fetchPageSize = 200;
        var fetchTotalPages = 1;

        do
        {
            var response = await _finePaymentsApi.GetHistoryAsync(memberKeyword, effectiveReceivedByKeyword, fromDate, toDate, fetchPage, fetchPageSize, cancellationToken);

            if (!response.Success)
            {
                ViewBag.Error = UiTextLocalizer.TranslateMessage(response.Message);
                return View("FinePaymentHistory", new List<FinePaymentHistoryVm>());
            }

            var pageData = response.Data.Deserialize<FinePaymentHistoryPageVm>(new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new FinePaymentHistoryPageVm();

            rows.AddRange(pageData.Items);
            fetchTotalPages = pageData.Pagination.TotalPages;
            fetchPage++;
        }
        while (fetchPage <= fetchTotalPages);

        ViewBag.Page = page;
        ViewBag.TotalPages = Math.Max(1, (int)Math.Ceiling(rows.Count / (double)safePageSize));
        ViewBag.TotalCount = rows.Count;
        ViewBag.PageSize = safePageSize;

        return View("FinePaymentHistory", rows);
    }

    [HttpGet]
    public async Task<IActionResult> ExportFinePaymentHistoryExcel(string? memberKeyword, string? receivedByKeyword, string? fromDate, string? toDate, CancellationToken cancellationToken)
    {
        var denyResult = RedirectIfNoFinePaymentAccess();
        if (denyResult is not null)
        {
            return denyResult;
        }

        var actorUsername = CurrentUsername();
        if (string.IsNullOrWhiteSpace(actorUsername))
        {
            return RedirectToAction("Login", "Auth");
        }

        var isStaff = IsStaff();
        var effectiveReceivedByKeyword = isStaff ? actorUsername : receivedByKeyword;

        var rows = new List<FinePaymentHistoryVm>();
        var page = 1;
        var pageSize = 200;
        var totalPages = 1;

        do
        {
            var response = await _finePaymentsApi.GetHistoryAsync(memberKeyword, effectiveReceivedByKeyword, fromDate, toDate, page, pageSize, cancellationToken);

            if (!response.Success)
            {
                TempData["Error"] = UiTextLocalizer.TranslateMessage(response.Message);
                return RedirectToAction(nameof(FinePaymentHistory), new { memberKeyword, receivedByKeyword = effectiveReceivedByKeyword, fromDate, toDate });
            }

            var pageData = response.Data.Deserialize<FinePaymentHistoryPageVm>(new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new FinePaymentHistoryPageVm();

            rows.AddRange(pageData.Items);
            totalPages = pageData.Pagination.TotalPages;
            page++;
        }
        while (page <= totalPages);

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("FinePaymentHistory");
        ws.Cell(1, 1).Value = "PaymentId";
        ws.Cell(1, 2).Value = "PaymentDate";
        ws.Cell(1, 3).Value = "MemberCode";
        ws.Cell(1, 4).Value = "MemberName";
        ws.Cell(1, 5).Value = "LoanId";
        ws.Cell(1, 6).Value = "AmountPaid";
        ws.Cell(1, 7).Value = "PaymentMethod";
        ws.Cell(1, 8).Value = "ReceivedByUsername";
        ws.Cell(1, 9).Value = "ReceivedByName";
        ws.Cell(1, 10).Value = "Note";

        for (var i = 0; i < rows.Count; i++)
        {
            var row = i + 2;
            ws.Cell(row, 1).Value = rows[i].PaymentId;
            ws.Cell(row, 2).Value = rows[i].PaymentDate.ToString("yyyy-MM-dd HH:mm:ss");
            ws.Cell(row, 3).Value = rows[i].MemberCode;
            ws.Cell(row, 4).Value = rows[i].MemberName;
            ws.Cell(row, 5).Value = rows[i].LoanId?.ToString() ?? string.Empty;
            ws.Cell(row, 6).Value = rows[i].AmountPaid;
            ws.Cell(row, 7).Value = rows[i].PaymentMethod;
            ws.Cell(row, 8).Value = rows[i].ReceivedByUsername;
            ws.Cell(row, 9).Value = rows[i].ReceivedByName;
            ws.Cell(row, 10).Value = rows[i].Note;
        }

        ws.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"fine-payment-history-{DateTime.Now:yyyyMMdd-HHmmss}.xlsx");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(string username, string fullName, string? email, string? phone, string roleName, string password, string confirmPassword, CancellationToken cancellationToken)
    {
        var denyResult = RedirectIfNotManager();
        if (denyResult is not null)
        {
            return denyResult;
        }

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(password))
        {
            return RedirectCreateWithError("Vui lòng nhập đầy đủ thông tin nhân viên.", username, fullName, email, phone, roleName);
        }

        if (!string.Equals(roleName, "staff", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(roleName, "manager", StringComparison.OrdinalIgnoreCase))
        {
            return RedirectCreateWithError("Vai trò không hợp lệ.", username, fullName, email, phone, roleName);
        }

        if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
        {
            return RedirectCreateWithError("Mật khẩu và xác nhận mật khẩu không khớp.", username, fullName, email, phone, roleName);
        }

        var actorUsername = CurrentUsername();
        if (string.IsNullOrWhiteSpace(actorUsername))
        {
            return RedirectToAction("Login", "Auth");
        }

        var response = await _usersApi.CreateAsync(new
        {
            ActorUsername = actorUsername,
            Username = username,
            FullName = fullName,
            Email = email,
            Phone = phone,
            RoleName = roleName,
            Password = password
        }, cancellationToken);

        if (!response.Success)
        {
            return RedirectCreateWithError(UiTextLocalizer.TranslateMessage(response.Message), username, fullName, email, phone, roleName);
        }

        TempData["Success"] = UiTextLocalizer.TranslateMessage(response.Message);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveEmployee(int userId, string fullName, string? email, string? phone, string roleName, bool isActive, string? newPassword, string? confirmNewPassword, CancellationToken cancellationToken)
    {
        var denyResult = RedirectIfNotManager();
        if (denyResult is not null)
        {
            return denyResult;
        }

        if (!string.Equals(roleName, "staff", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(roleName, "manager", StringComparison.OrdinalIgnoreCase))
        {
            return RedirectEditWithError("Vai trò không hợp lệ.", userId, fullName, email, phone, roleName, isActive);
        }

        if (!string.IsNullOrWhiteSpace(newPassword) || !string.IsNullOrWhiteSpace(confirmNewPassword))
        {
            if (string.IsNullOrWhiteSpace(newPassword) || string.IsNullOrWhiteSpace(confirmNewPassword))
            {
                return RedirectEditWithError("Vui lòng nhập đầy đủ mật khẩu mới và xác nhận mật khẩu.", userId, fullName, email, phone, roleName, isActive);
            }

            if (!string.Equals(newPassword, confirmNewPassword, StringComparison.Ordinal))
            {
                return RedirectEditWithError("Mật khẩu mới và xác nhận mật khẩu mới không khớp.", userId, fullName, email, phone, roleName, isActive);
            }
        }

        var actorUsername = CurrentUsername();
        if (string.IsNullOrWhiteSpace(actorUsername))
        {
            return RedirectToAction("Login", "Auth");
        }

        var updateResponse = await _usersApi.UpdateAsync(userId, new
        {
            ActorUsername = actorUsername,
            UserId = userId,
            FullName = fullName,
            Email = email,
            Phone = phone,
            RoleName = roleName,
            IsActive = isActive
        }, cancellationToken);

        if (!updateResponse.Success)
        {
            return RedirectEditWithError(UiTextLocalizer.TranslateMessage(updateResponse.Message), userId, fullName, email, phone, roleName, isActive);
        }

        if (!string.IsNullOrWhiteSpace(newPassword))
        {
            var passwordResponse = await _usersApi.ResetPasswordAsync(userId, new
            {
                ActorUsername = actorUsername,
                UserId = userId,
                NewPassword = newPassword
            }, cancellationToken);

            if (!passwordResponse.Success)
            {
                return RedirectEditWithError(UiTextLocalizer.TranslateMessage(passwordResponse.Message), userId, fullName, email, phone, roleName, isActive);
            }

            TempData["Success"] = "Cập nhật nhân viên và đổi mật khẩu thành công.";
            return RedirectToAction(nameof(Index));
        }

        TempData["Success"] = UiTextLocalizer.TranslateMessage(updateResponse.Message);
        return RedirectToAction(nameof(Index));
    }
}


