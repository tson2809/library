namespace Client_web.Models.ViewModels;

public sealed class MemberStatusVm
{
    public int MemberId { get; set; }

    public string MemberCode { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public int BorrowingLoans { get; set; }

    public int OverdueLoans { get; set; }

    public decimal TotalFine { get; set; }

    public decimal TotalPaid { get; set; }

    public decimal OutstandingFine { get; set; }

    public bool LoanBlocked { get; set; }
}
