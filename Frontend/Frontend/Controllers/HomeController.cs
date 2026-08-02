using System.Diagnostics;
using Client_web.Models;
using System.Text.Json;
using Client_web.Models.ViewModels;
using Client_web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Client_web.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IManagerApiClient _managerApi;
        private readonly IFinePaymentsApiClient _finePaymentsApi;

        public HomeController(ILogger<HomeController> logger, IManagerApiClient managerApi, IFinePaymentsApiClient finePaymentsApi)
        {
            _logger = logger;
            _managerApi = managerApi;
            _finePaymentsApi = finePaymentsApi;
        }

        public IActionResult Index()
        {
            var role = HttpContext.Session.GetString("Auth.Role");
            if (string.Equals(role, "Quản lý", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction(nameof(Dashboard));
            }

            return RedirectToAction("Index", "Books");
        }

        [HttpGet]
        public async Task<IActionResult> Dashboard(CancellationToken cancellationToken)
        {
            var role = HttpContext.Session.GetString("Auth.Role");
            if (!string.Equals(role, "Quản lý", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("Index", "Books");
            }

            var actorUsername = HttpContext.Session.GetString("Auth.Username") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(actorUsername))
            {
                return RedirectToAction("Login", "Auth");
            }

            var response = await _managerApi.GetDashboardAsync(cancellationToken);
            if (!response.Success)
            {
                ViewBag.Error = UiTextLocalizer.TranslateMessage(response.Message);
                return View(new DashboardVm());
            }

            var vm = response.Data.Deserialize<DashboardVm>(new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new DashboardVm();

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CollectFine(string? memberCode, int? loanId, decimal amountPaid, string? paymentMethod, string? note, CancellationToken cancellationToken)
        {
            var role = HttpContext.Session.GetString("Auth.Role");
            if (!string.Equals(role, "Nhân viên", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("FinePaymentHistory", "Manager");
            }

            var actorUsername = HttpContext.Session.GetString("Auth.Username") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(actorUsername))
            {
                return RedirectToAction("Login", "Auth");
            }

            var response = await _finePaymentsApi.CollectAsync(new
            {
                ActorUsername = actorUsername,
                MemberCode = memberCode,
                LoanId = loanId,
                AmountPaid = amountPaid,
                PaymentMethod = paymentMethod,
                Note = note,
                ReceivedByUsername = actorUsername
            }, cancellationToken);

            TempData[response.Success ? "Success" : "Error"] = UiTextLocalizer.TranslateMessage(response.Message);
            return RedirectToAction("FinePaymentHistory", "Manager");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}


