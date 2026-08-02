namespace Client_web.Models.ViewModels;

public sealed class OverdueLoanVm
{
    public int LoanId { get; set; }

    public string Member { get; set; } = string.Empty;

    public DateOnly LoanDate { get; set; }

    public DateOnly DueDate { get; set; }

    public int DaysOverdue { get; set; }

    public string Status { get; set; } = string.Empty;

    public int ItemCount { get; set; }
}
