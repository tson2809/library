using System.Text.Json;
using System.Globalization;
using Client_web.Models.ViewModels;
using Client_web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Client_web.Controllers;

public sealed class LoansController : Controller
{
    private readonly ILoansApiClient _loansApi;
    private readonly IReservationsApiClient _reservationsApi;
    private readonly IMembersApiClient _membersApi;
    private readonly IBookCopiesApiClient _bookCopiesApi;
    private const string LoanListViewName = "LoanList";

    public LoansController(ILoansApiClient loansApi, IReservationsApiClient reservationsApi, IMembersApiClient membersApi, IBookCopiesApiClient bookCopiesApi)
    {
        _loansApi = loansApi;
        _reservationsApi = reservationsApi;
        _membersApi = membersApi;
        _bookCopiesApi = bookCopiesApi;
    }

    private bool IsManager()
    {
        var role = HttpContext.Session.GetString("Auth.Role");
        return string.Equals(role, "Quản lý", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (IsManager())
        {
            return RedirectToAction("Index", "Manager");
        }

        var actorUsername = HttpContext.Session.GetString("Auth.Username") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(actorUsername))
        {
            return RedirectToAction("Login", "Auth");
        }

        var response = await _loansApi.GetLoansAsync(cancellationToken);
        if (!response.Success)
        {
            ViewBag.Error = UiTextLocalizer.TranslateMessage(response.Message);
            return View(LoanListViewName, new List<LoanVm>());
        }

        var items = response.Data.Deserialize<List<LoanVm>>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new List<LoanVm>();

        var membersResponse = await _membersApi.GetMembersAsync(cancellationToken);
        var members = membersResponse.Success
            ? membersResponse.Data.Deserialize<List<MemberVm>>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<MemberVm>()
            : new List<MemberVm>();

        var copiesResponse = await _bookCopiesApi.LookupAsync(string.Empty, cancellationToken);
        var copies = copiesResponse.Success
            ? copiesResponse.Data.Deserialize<List<BookCopyLookupVm>>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<BookCopyLookupVm>()
            : new List<BookCopyLookupVm>();

        var reservationsResponse = await _reservationsApi.GetOpenReservationsAsync(cancellationToken);

        var reservations = reservationsResponse.Success
            ? reservationsResponse.Data.Deserialize<List<LoanReservationVm>>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<LoanReservationVm>()
            : new List<LoanReservationVm>();

        ViewBag.MemberSeedJson = JsonSerializer.Serialize(members);
        ViewBag.CopySeedJson = JsonSerializer.Serialize(copies);
        ViewBag.Reservations = reservations;

        return View(LoanListViewName, items);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateReservation(string? memberCode, int? bookId, string? note, CancellationToken cancellationToken)
    {
        if (IsManager())
        {
            return RedirectToAction("Index", "Manager");
        }

        var actorUsername = HttpContext.Session.GetString("Auth.Username") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(actorUsername))
        {
            TempData["Error"] = "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.";
            return RedirectToAction("Login", "Auth");
        }

        if (string.IsNullOrWhiteSpace(memberCode) || !bookId.HasValue || bookId.Value <= 0)
        {
            TempData["Error"] = "Vui lòng chọn thành viên và đầu sách cần đặt trước.";
            return RedirectToAction(nameof(Index));
        }

        var response = await _reservationsApi.CreateAsync(new
        {
            ActorUsername = actorUsername,
            MemberCode = memberCode,
            BookId = bookId.Value,
            Note = note
        }, cancellationToken);

        TempData[response.Success ? "Success" : "Error"] = UiTextLocalizer.TranslateMessage(response.Message);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelReservation(int reservationId, CancellationToken cancellationToken)
    {
        if (IsManager())
        {
            return RedirectToAction("Index", "Manager");
        }

        var actorUsername = HttpContext.Session.GetString("Auth.Username") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(actorUsername))
        {
            TempData["Error"] = "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.";
            return RedirectToAction("Login", "Auth");
        }

        if (reservationId <= 0)
        {
            TempData["Error"] = "Thiếu mã đặt trước.";
            return RedirectToAction(nameof(Index));
        }

        var response = await _reservationsApi.CancelAsync(reservationId, cancellationToken);

        TempData[response.Success ? "Success" : "Error"] = UiTextLocalizer.TranslateMessage(response.Message);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string? memberCode, string? dueDate, string? selectedBarcodes, string? note, CancellationToken cancellationToken)
    {
        if (IsManager())
        {
            return RedirectToAction("Index", "Manager");
        }

        var barcodeList = (selectedBarcodes ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (string.IsNullOrWhiteSpace(memberCode) || barcodeList.Length == 0)
        {
            TempData["CreateLoanError"] = "Vui lòng thêm ít nhất 1 sản phẩm.";
            return RedirectToAction(nameof(Index));
        }

        if (!DateOnly.TryParse(dueDate, out var parsedDueDate))
        {
            TempData["CreateLoanError"] = "Vui lòng chọn ngày trả hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        if (parsedDueDate <= DateOnly.FromDateTime(DateTime.Today))
        {
            TempData["CreateLoanError"] = "Ngày trả phải sau ngày hiện tại.";
            return RedirectToAction(nameof(Index));
        }

        var processedByUsername = HttpContext.Session.GetString("Auth.Username") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(processedByUsername))
        {
            TempData["CreateLoanError"] = "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.";
            return RedirectToAction("Login", "Auth");
        }

        var response = await _loansApi.CreateAsync(new
        {
            MemberCode = memberCode,
            ProcessedByUsername = processedByUsername,
            DueDate = parsedDueDate.ToString("yyyy-MM-dd"),
            Barcodes = barcodeList,
            ConditionBefore = "Good",
            Note = note
        }, cancellationToken);

        if (response.Success)
        {
            TempData["Success"] = UiTextLocalizer.TranslateMessage(response.Message);
        }
        else
        {
            TempData["CreateLoanError"] = UiTextLocalizer.TranslateMessage(response.Message);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReturnSelected(string? barcodes, string? conditionAfters, CancellationToken cancellationToken)
    {
        if (IsManager())
        {
            return RedirectToAction("Index", "Manager");
        }

        var barcodeList = (barcodes ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var conditionList = (conditionAfters ?? string.Empty)
            .Split(',', StringSplitOptions.None)
            .Select(x => x.Trim())
            .ToArray();

        if (barcodeList.Length == 0)
        {
            TempData["Error"] = "Vui lòng chọn ít nhất 1 cuốn sách để trả.";
            return RedirectToAction(nameof(Index));
        }

        var successCount = 0;
        var firstError = string.Empty;
        var firstSuccessMessage = string.Empty;

        for (var i = 0; i < barcodeList.Length; i++)
        {
            var conditionAfter = i < conditionList.Length ? conditionList[i] : null;
            var response = await _loansApi.ReturnByBarcodeAsync(new
            {
                ActorUsername = HttpContext.Session.GetString("Auth.Username") ?? string.Empty,
                Barcode = barcodeList[i],
                ConditionAfter = conditionAfter
            }, cancellationToken);

            if (response.Success)
            {
                successCount++;
                if (string.IsNullOrWhiteSpace(firstSuccessMessage))
                {
                    firstSuccessMessage = UiTextLocalizer.TranslateMessage(response.Message);
                }
            }
            else if (string.IsNullOrWhiteSpace(firstError))
            {
                firstError = UiTextLocalizer.TranslateMessage(response.Message);
            }
        }

        if (successCount > 0)
        {
            TempData["Success"] = successCount == 1
                ? (string.IsNullOrWhiteSpace(firstSuccessMessage) ? "Trả sách thành công." : firstSuccessMessage)
                : $"Trả sách thành công {successCount} cuốn.";
        }

        if (!string.IsNullOrWhiteSpace(firstError))
        {
            TempData["Error"] = firstError;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Renew(int loanId, string? newDueDate, CancellationToken cancellationToken)
    {
        if (IsManager())
        {
            return RedirectToAction("Index", "Manager");
        }

        if (!DateOnly.TryParse(newDueDate, out var parsedNewDueDate))
        {
            TempData["Error"] = "Vui lòng chọn ngày gia hạn hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        var response = await _loansApi.RenewAsync(loanId, new
        {
            ActorUsername = HttpContext.Session.GetString("Auth.Username") ?? string.Empty,
            LoanId = loanId,
            NewDueDate = parsedNewDueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        }, cancellationToken);

        TempData[response.Success ? "Success" : "Error"] = UiTextLocalizer.TranslateMessage(response.Message);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> DetailData(int loanId, CancellationToken cancellationToken)
    {
        if (IsManager())
        {
            return Json(new { success = false, message = "Quản lý không có quyền truy cập chi tiết phiếu mượn." });
        }

        var response = await _loansApi.GetDetailAsync(loanId, cancellationToken);
        if (!response.Success)
        {
            return Json(new { success = false, message = UiTextLocalizer.TranslateMessage(response.Message) });
        }

        var detail = response.Data.Deserialize<LoanDetailVm>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (detail is null)
        {
            return Json(new { success = false, message = "Không đọc được dữ liệu chi tiết phiếu mượn." });
        }

        return Json(new { success = true, data = detail });
    }

    [HttpGet]
    public async Task<IActionResult> PrintReceipt(int loanId, CancellationToken cancellationToken)
    {
        if (IsManager())
        {
            TempData["Error"] = "Quản lý không có quyền in phiếu mượn/trả.";
            return RedirectToAction("Index", "Manager");
        }

        var response = await _loansApi.GetDetailAsync(loanId, cancellationToken);

        if (!response.Success)
        {
            TempData["Error"] = UiTextLocalizer.TranslateMessage(response.Message);
            return RedirectToAction(nameof(Index));
        }

        var detail = response.Data.Deserialize<LoanDetailVm>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (detail is null)
        {
            TempData["Error"] = "Không đọc được dữ liệu phiếu mượn để in.";
            return RedirectToAction(nameof(Index));
        }

        return View("Receipt", detail);
    }

    [HttpGet]
    public async Task<IActionResult> MemberStatusData(string? memberCode, CancellationToken cancellationToken)
    {
        if (IsManager())
        {
            return Json(new { success = false, message = "Quản lý không có quyền truy cập dữ liệu công nợ thành viên." });
        }

        if (string.IsNullOrWhiteSpace(memberCode))
        {
            return Json(new { success = false, message = "Thiếu mã thành viên." });
        }

        var actorUsername = HttpContext.Session.GetString("Auth.Username") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(actorUsername))
        {
            return Json(new { success = false, message = "Phiên đăng nhập đã hết hạn." });
        }

        var response = await _membersApi.GetStatusAsync(memberCode, cancellationToken);

        if (!response.Success)
        {
            return Json(new { success = false, message = UiTextLocalizer.TranslateMessage(response.Message) });
        }

        var status = response.Data.Deserialize<MemberStatusVm>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (status is null)
        {
            return Json(new { success = false, message = "Không đọc được dữ liệu trạng thái thành viên." });
        }

        return Json(new { success = true, data = status });
    }
}




