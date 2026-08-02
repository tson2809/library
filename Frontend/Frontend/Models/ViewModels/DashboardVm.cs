namespace Client_web.Models.ViewModels;

public sealed class DashboardVm
{
    public int TotalBookTitles { get; set; }

    public int TotalBookCopies { get; set; }

    public int BorrowedCopies { get; set; }

    public int ActiveMembers { get; set; }

    public int TodayLoans { get; set; }

    public int OverdueLoans { get; set; }

    public int LostOrDamagedCopies { get; set; }

    public decimal OutstandingFine { get; set; }

    public decimal CollectedFineThisMonth { get; set; }

    public int RevenueYear { get; set; } = DateTime.Today.Year;

    public decimal RevenueThisYear { get; set; }

    public decimal RevenueLastMonth { get; set; }

    public List<MonthlyRevenueVm> MonthlyRevenues { get; set; } = new();

    public List<InventoryAlertVm> InventoryAlerts { get; set; } = new();
}

public sealed class InventoryAlertVm
{
    public string AlertType { get; set; } = string.Empty;

    public string ItemCode { get; set; } = string.Empty;

    public string ItemName { get; set; } = string.Empty;

    public string Detail { get; set; } = string.Empty;
}
