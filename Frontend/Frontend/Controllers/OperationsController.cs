using System.Globalization;
using System.Text.Json;
using Client_web.Models.ViewModels;
using Client_web.Services;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;

namespace Client_web.Controllers;

public sealed class OperationsController : Controller
{
    private readonly IBooksApiClient _booksApi;
    private readonly ILoansApiClient _loansApi;
    private readonly IManagerApiClient _managerApi;
    private readonly IFinePaymentsApiClient _finePaymentsApi;
    private readonly IReportsApiClient _reportsApi;
    private readonly ISystemLogsApiClient _systemLogsApi;

    public OperationsController(IBooksApiClient booksApi, ILoansApiClient loansApi, IManagerApiClient managerApi, IFinePaymentsApiClient finePaymentsApi, IReportsApiClient reportsApi, ISystemLogsApiClient systemLogsApi)
    {
        _booksApi = booksApi;
        _loansApi = loansApi;
        _managerApi = managerApi;
        _finePaymentsApi = finePaymentsApi;
        _reportsApi = reportsApi;
        _systemLogsApi = systemLogsApi;
    }

    private bool IsManager()
    {
        var role = HttpContext.Session.GetString("Auth.Role");
        return string.Equals(role, "Quản lý", StringComparison.OrdinalIgnoreCase);
    }

    private IActionResult? RedirectIfNotManager()
    {
        if (IsManager())
        {
            return null;
        }

        return RedirectToAction("Index", "Home");
    }

    private string? CurrentUsername()
    {
        return HttpContext.Session.GetString("Auth.Username");
    }

    [HttpGet]
    public async Task<IActionResult> Reports(int? year, CancellationToken cancellationToken)
    {
        var deny = RedirectIfNotManager();
        if (deny is not null)
        {
            return deny;
        }

        var actorUsername = CurrentUsername();
        if (string.IsNullOrWhiteSpace(actorUsername))
        {
            return RedirectToAction("Login", "Auth");
        }

        var booksResponse = await _booksApi.GetBooksAsync(cancellationToken);
        var loansResponse = await _loansApi.GetLoansAsync(cancellationToken);
        var revenueYear = year ?? DateTime.Today.Year;
        var revenueResponse = await _managerApi.GetRevenueAsync(revenueYear, cancellationToken);

        var books = new List<BookVm>();
        var loans = new List<LoanVm>();

        if (booksResponse.Success)
        {
            books = booksResponse.Data.Deserialize<List<BookVm>>(new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new List<BookVm>();
        }
        else
        {
            ViewBag.Error = UiTextLocalizer.TranslateMessage(booksResponse.Message);
        }

        if (loansResponse.Success)
        {
            loans = loansResponse.Data.Deserialize<List<LoanVm>>(new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new List<LoanVm>();
        }

        var topBooks = books
            .OrderByDescending(b => b.BorrowCount)
            .ThenBy(b => b.Title)
            .Take(20)
            .Select(b => new TopBookVm
            {
                Title = b.Title,
                Isbn = b.Isbn,
                BorrowCount = b.BorrowCount,
                AvailableCopies = b.AvailableCopies,
                TotalCopies = b.TotalCopies
            })
            .ToList();

        var topMembers = loans
            .Where(l => !string.IsNullOrWhiteSpace(l.Member))
            .GroupBy(l => l.Member.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => new TopMemberVm
            {
                Name = g.Key,
                LoanCount = g.Count()
            })
            .OrderByDescending(x => x.LoanCount)
            .ThenBy(x => x.Name)
            .Take(20)
            .ToList();

        var memberBorrowRows = loans
            .Where(l => !string.IsNullOrWhiteSpace(l.Member))
            .GroupBy(l => l.Member.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var titles = g
                    .SelectMany(l => l.BookTitles ?? new List<string>())
                    .Select(title => title?.Trim() ?? string.Empty)
                    .Where(title => !string.IsNullOrWhiteSpace(title))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(title => title)
                    .Take(20)
                    .ToList();

                var recentLoans = g
                    .OrderByDescending(l => l.LoanId)
                    .Take(8)
                    .Select(l => new MemberLoanSnapshotVm
                    {
                        LoanId = l.LoanId,
                        LoanDate = l.LoanDate,
                        DueDate = l.DueDate,
                        Status = string.IsNullOrWhiteSpace(l.StatusText) ? l.Status : l.StatusText,
                        ItemCount = l.ItemCount,
                        BookTitles = (l.BookTitles ?? new List<string>())
                            .Where(title => !string.IsNullOrWhiteSpace(title))
                            .Take(8)
                            .ToList()
                    })
                    .ToList();

                return new MemberBorrowReportRowVm
                {
                    Name = g.Key,
                    LoanCount = g.Count(),
                    TotalBorrowedItems = g.Sum(x => x.ItemCount),
                    BorrowedTitles = titles,
                    RecentLoans = recentLoans
                };
            })
            .OrderByDescending(x => x.LoanCount)
            .ThenByDescending(x => x.TotalBorrowedItems)
            .ThenBy(x => x.Name)
            .Take(20)
            .ToList();

        var frequentBorrowers = topMembers
            .Where(x => x.LoanCount >= 5)
            .OrderByDescending(x => x.LoanCount)
            .ToList();

        var monthlyRevenues = new List<MonthlyRevenueVm>();
        if (revenueResponse.Success)
        {
            if (revenueResponse.Data.ValueKind == JsonValueKind.Object
                && revenueResponse.Data.TryGetProperty("items", out var itemsElement)
                && itemsElement.ValueKind == JsonValueKind.Array)
            {
                monthlyRevenues = itemsElement.Deserialize<List<MonthlyRevenueVm>>(new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new List<MonthlyRevenueVm>();
            }
        }
        else
        {
            ViewBag.RevenueError = UiTextLocalizer.TranslateMessage(revenueResponse.Message);
        }

        var finePayments = new List<FinePaymentHistoryVm>();
        var paymentPage = 1;
        const int paymentPageSize = 200;
        var paymentTotalPages = 1;

        do
        {
            var paymentResponse = await _finePaymentsApi.GetHistoryAsync(null, null, null, null, paymentPage, paymentPageSize, cancellationToken);

            if (!paymentResponse.Success)
            {
                break;
            }

            var paymentPageData = paymentResponse.Data.Deserialize<FinePaymentHistoryPageVm>(new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new FinePaymentHistoryPageVm();

            finePayments.AddRange(paymentPageData.Items);
            paymentTotalPages = paymentPageData.Pagination.TotalPages;
            paymentPage++;
        }
        while (paymentPage <= paymentTotalPages);

        static bool HasBookTitle(LoanVm loan, string title)
        {
            if (loan.BookTitles is null || loan.BookTitles.Count == 0 || string.IsNullOrWhiteSpace(title))
            {
                return false;
            }

            return loan.BookTitles.Any(bookTitle => string.Equals(bookTitle, title, StringComparison.OrdinalIgnoreCase));
        }

        var activeLoans = loans
            .Where(l => string.Equals(l.Status, "Borrowing", StringComparison.OrdinalIgnoreCase)
                || string.Equals(l.Status, "Overdue", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var book in topBooks)
        {
            book.ActiveBorrowers = activeLoans
                .Where(loan => HasBookTitle(loan, book.Title))
                .Select(loan => new TopBookBorrowerVm
                {
                    MemberName = loan.Member,
                    LoanId = loan.LoanId,
                    DueDate = loan.DueDate,
                    Status = string.IsNullOrWhiteSpace(loan.StatusText) ? loan.Status : loan.StatusText
                })
                .OrderBy(loan => loan.DueDate)
                .ThenBy(loan => loan.MemberName)
                .Take(50)
                .ToList();
        }

        var loanTitlesByLoanId = loans
            .GroupBy(l => l.LoanId)
            .ToDictionary(
                g => g.Key,
                g => g.SelectMany(x => x.BookTitles ?? new List<string>())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x)
                    .ToList());

        foreach (var monthItem in monthlyRevenues)
        {
            monthItem.FineDetails = finePayments
                .Where(p => p.PaymentDate.Year == revenueYear && p.PaymentDate.Month == monthItem.Month)
                .OrderByDescending(p => p.PaymentDate)
                .Take(100)
                .Select(p => new MonthlyFineDetailVm
                {
                    PaymentDate = p.PaymentDate,
                    MemberName = p.MemberName,
                    MemberCode = p.MemberCode,
                    AmountPaid = p.AmountPaid,
                    LoanId = p.LoanId,
                    BookTitles = p.LoanId.HasValue && loanTitlesByLoanId.TryGetValue(p.LoanId.Value, out var titles)
                        ? titles
                        : new List<string>()
                })
                .ToList();
        }

        var vm = new ManagerReportsVm
        {
            BorrowingLoanCount = loans.Count(l => string.Equals(l.Status, "Borrowing", StringComparison.OrdinalIgnoreCase)),
            OverdueLoanCount = loans.Count(l => string.Equals(l.Status, "Overdue", StringComparison.OrdinalIgnoreCase)),
            TotalAvailableCopies = books.Sum(b => b.AvailableCopies),
            TotalBorrowedCopies = books.Sum(b => Math.Max(0, b.TotalCopies - b.AvailableCopies)),
            TopBorrowedBooks = topBooks,
            TopMembers = topMembers,
            MemberBorrowRows = memberBorrowRows,
            FrequentBorrowers = frequentBorrowers,
            RevenueYear = revenueYear,
            MonthlyRevenues = monthlyRevenues.OrderBy(x => x.Month).ToList()
        };

        ViewBag.LoansJson = JsonSerializer.Serialize(loans);

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> OverdueLoans(CancellationToken cancellationToken)
    {
        var deny = RedirectIfNotManager();
        if (deny is not null)
        {
            return deny;
        }

        var actorUsername = CurrentUsername();
        if (string.IsNullOrWhiteSpace(actorUsername))
        {
            return RedirectToAction("Login", "Auth");
        }

        var response = await _reportsApi.GetOverdueLoansAsync(cancellationToken);
        if (!response.Success)
        {
            ViewBag.Error = UiTextLocalizer.TranslateMessage(response.Message);
            return View(new List<OverdueLoanVm>());
        }

        var items = response.Data.Deserialize<List<OverdueLoanVm>>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new List<OverdueLoanVm>();

        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> ExportOverdueExcel(CancellationToken cancellationToken)
    {
        var deny = RedirectIfNotManager();
        if (deny is not null)
        {
            return deny;
        }

        var actorUsername = CurrentUsername();
        if (string.IsNullOrWhiteSpace(actorUsername))
        {
            return RedirectToAction("Login", "Auth");
        }

        var response = await _reportsApi.GetOverdueLoansAsync(cancellationToken);
        if (!response.Success)
        {
            TempData["Error"] = UiTextLocalizer.TranslateMessage(response.Message);
            return RedirectToAction(nameof(OverdueLoans));
        }

        var items = response.Data.Deserialize<List<OverdueLoanVm>>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new List<OverdueLoanVm>();

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("OverdueLoans");
        ws.Cell(1, 1).Value = "LoanId";
        ws.Cell(1, 2).Value = "Member";
        ws.Cell(1, 3).Value = "LoanDate";
        ws.Cell(1, 4).Value = "DueDate";
        ws.Cell(1, 5).Value = "DaysOverdue";
        ws.Cell(1, 6).Value = "ItemCount";
        ws.Cell(1, 7).Value = "Status";

        for (var i = 0; i < items.Count; i++)
        {
            var row = i + 2;
            ws.Cell(row, 1).Value = items[i].LoanId;
            ws.Cell(row, 2).Value = items[i].Member;
            ws.Cell(row, 3).Value = items[i].LoanDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            ws.Cell(row, 4).Value = items[i].DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            ws.Cell(row, 5).Value = items[i].DaysOverdue;
            ws.Cell(row, 6).Value = items[i].ItemCount;
            ws.Cell(row, 7).Value = items[i].Status;
        }

        ws.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"overdue-loans-{DateTime.Now:yyyyMMdd-HHmmss}.xlsx");
    }

    [HttpGet]
    public async Task<IActionResult> OverdueLoansPrint(CancellationToken cancellationToken)
    {
        var deny = RedirectIfNotManager();
        if (deny is not null)
        {
            return deny;
        }

        var actorUsername = CurrentUsername();
        if (string.IsNullOrWhiteSpace(actorUsername))
        {
            return RedirectToAction("Login", "Auth");
        }

        var response = await _reportsApi.GetOverdueLoansAsync(cancellationToken);
        if (!response.Success)
        {
            TempData["Error"] = UiTextLocalizer.TranslateMessage(response.Message);
            return RedirectToAction(nameof(OverdueLoans));
        }

        var items = response.Data.Deserialize<List<OverdueLoanVm>>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new List<OverdueLoanVm>();

        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> SystemLogs(CancellationToken cancellationToken)
    {
        var deny = RedirectIfNotManager();
        if (deny is not null)
        {
            return deny;
        }

        var actorUsername = CurrentUsername();
        if (string.IsNullOrWhiteSpace(actorUsername))
        {
            return RedirectToAction("Login", "Auth");
        }

        var response = await _systemLogsApi.GetSystemLogsAsync(cancellationToken);
        if (!response.Success)
        {
            ViewBag.Error = UiTextLocalizer.TranslateMessage(response.Message);
            return View(new List<SystemLogVm>());
        }

        var items = response.Data.Deserialize<List<SystemLogVm>>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new List<SystemLogVm>();

        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> ExportSystemLogsExcel(CancellationToken cancellationToken)
    {
        var deny = RedirectIfNotManager();
        if (deny is not null)
        {
            return deny;
        }

        var actorUsername = CurrentUsername();
        if (string.IsNullOrWhiteSpace(actorUsername))
        {
            return RedirectToAction("Login", "Auth");
        }

        var response = await _systemLogsApi.GetSystemLogsAsync(cancellationToken);
        if (!response.Success)
        {
            TempData["Error"] = UiTextLocalizer.TranslateMessage(response.Message);
            return RedirectToAction(nameof(SystemLogs));
        }

        var items = response.Data.Deserialize<List<SystemLogVm>>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new List<SystemLogVm>();

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("SystemLogs");
        ws.Cell(1, 1).Value = "LogId";
        ws.Cell(1, 2).Value = "CreatedAt";
        ws.Cell(1, 3).Value = "UserName";
        ws.Cell(1, 4).Value = "ActionType";
        ws.Cell(1, 5).Value = "EntityName";
        ws.Cell(1, 6).Value = "EntityId";
        ws.Cell(1, 7).Value = "Description";

        for (var i = 0; i < items.Count; i++)
        {
            var row = i + 2;
            ws.Cell(row, 1).Value = items[i].LogId;
            ws.Cell(row, 2).Value = items[i].CreatedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            ws.Cell(row, 3).Value = items[i].UserName;
            ws.Cell(row, 4).Value = items[i].ActionType;
            ws.Cell(row, 5).Value = items[i].EntityName;
            ws.Cell(row, 6).Value = items[i].EntityId;
            ws.Cell(row, 7).Value = items[i].Description;
        }

        ws.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"system-logs-{DateTime.Now:yyyyMMdd-HHmmss}.xlsx");
    }

    [HttpGet]
    public async Task<IActionResult> ExportTopBooksExcel(CancellationToken cancellationToken)
    {
        var deny = RedirectIfNotManager();
        if (deny is not null)
        {
            return deny;
        }

        var actorUsername = CurrentUsername();
        if (string.IsNullOrWhiteSpace(actorUsername))
        {
            return RedirectToAction("Login", "Auth");
        }

        var booksResponse = await _booksApi.GetBooksAsync(cancellationToken);
        var loansResponse = await _loansApi.GetLoansAsync(cancellationToken);

        if (!booksResponse.Success)
        {
            TempData["Error"] = UiTextLocalizer.TranslateMessage(booksResponse.Message);
            return RedirectToAction(nameof(Reports));
        }

        if (!loansResponse.Success)
        {
            TempData["Error"] = UiTextLocalizer.TranslateMessage(loansResponse.Message);
            return RedirectToAction(nameof(Reports));
        }

        var books = booksResponse.Data.Deserialize<List<BookVm>>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new List<BookVm>();

        var loans = loansResponse.Data.Deserialize<List<LoanVm>>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new List<LoanVm>();

        using var workbook = new XLWorkbook();

        var topBooksSheet = workbook.Worksheets.Add("TopBooks");
        topBooksSheet.Cell(1, 1).Value = "Title";
        topBooksSheet.Cell(1, 2).Value = "ISBN";
        topBooksSheet.Cell(1, 3).Value = "BorrowCount";
        topBooksSheet.Cell(1, 4).Value = "AvailableCopies";
        topBooksSheet.Cell(1, 5).Value = "TotalCopies";

        var topBooks = books.OrderByDescending(b => b.BorrowCount).ThenBy(b => b.Title).Take(200).ToList();
        for (var i = 0; i < topBooks.Count; i++)
        {
            var row = i + 2;
            topBooksSheet.Cell(row, 1).Value = topBooks[i].Title;
            topBooksSheet.Cell(row, 2).Value = topBooks[i].Isbn;
            topBooksSheet.Cell(row, 3).Value = topBooks[i].BorrowCount;
            topBooksSheet.Cell(row, 4).Value = topBooks[i].AvailableCopies;
            topBooksSheet.Cell(row, 5).Value = topBooks[i].TotalCopies;
        }

        var topMembersSheet = workbook.Worksheets.Add("TopMembers");
        topMembersSheet.Cell(1, 1).Value = "MemberName";
        topMembersSheet.Cell(1, 2).Value = "LoanCount";

        var topMembers = loans
            .Where(l => !string.IsNullOrWhiteSpace(l.Member))
            .GroupBy(l => l.Member.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => new { Name = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Name)
            .Take(200)
            .ToList();

        for (var i = 0; i < topMembers.Count; i++)
        {
            var row = i + 2;
            topMembersSheet.Cell(row, 1).Value = topMembers[i].Name;
            topMembersSheet.Cell(row, 2).Value = topMembers[i].Count;
        }

        topBooksSheet.Columns().AdjustToContents();
        topMembersSheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"library-reports-{DateTime.Now:yyyyMMdd-HHmmss}.xlsx");
    }

    [HttpGet]
    public async Task<IActionResult> ExportMonthlyRevenueExcel(int? year, CancellationToken cancellationToken)
    {
        var deny = RedirectIfNotManager();
        if (deny is not null)
        {
            return deny;
        }

        var actorUsername = CurrentUsername();
        if (string.IsNullOrWhiteSpace(actorUsername))
        {
            return RedirectToAction("Login", "Auth");
        }

        var revenueYear = year ?? DateTime.Today.Year;
        var response = await _managerApi.GetRevenueAsync(revenueYear, cancellationToken);

        if (!response.Success)
        {
            TempData["Error"] = UiTextLocalizer.TranslateMessage(response.Message);
            return RedirectToAction(nameof(Reports), new { year = revenueYear });
        }

        var items = new List<MonthlyRevenueVm>();
        if (response.Data.ValueKind == JsonValueKind.Object
            && response.Data.TryGetProperty("items", out var itemsElement)
            && itemsElement.ValueKind == JsonValueKind.Array)
        {
            items = itemsElement.Deserialize<List<MonthlyRevenueVm>>(new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new List<MonthlyRevenueVm>();
        }

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("MonthlyRevenue");
        ws.Cell(1, 1).Value = "Year";
        ws.Cell(1, 2).Value = "Month";
        ws.Cell(1, 3).Value = "TotalFineCollected";
        ws.Cell(1, 4).Value = "PaymentCount";

        var ordered = items.OrderBy(x => x.Month).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            var row = i + 2;
            ws.Cell(row, 1).Value = revenueYear;
            ws.Cell(row, 2).Value = ordered[i].Month;
            ws.Cell(row, 3).Value = ordered[i].TotalFineCollected;
            ws.Cell(row, 4).Value = ordered[i].PaymentCount;
        }

        ws.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"monthly-revenue-{revenueYear}-{DateTime.Now:yyyyMMdd-HHmmss}.xlsx");
    }
}
