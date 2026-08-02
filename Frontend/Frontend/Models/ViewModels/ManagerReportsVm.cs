namespace Client_web.Models.ViewModels;

public sealed class ManagerReportsVm
{
    public int BorrowingLoanCount { get; set; }

    public int OverdueLoanCount { get; set; }

    public int TotalAvailableCopies { get; set; }

    public int TotalBorrowedCopies { get; set; }

    public List<TopBookVm> TopBorrowedBooks { get; set; } = new();

    public List<TopMemberVm> TopMembers { get; set; } = new();

    public List<MemberBorrowReportRowVm> MemberBorrowRows { get; set; } = new();

    public List<TopMemberVm> FrequentBorrowers { get; set; } = new();

    public int RevenueYear { get; set; }

    public List<MonthlyRevenueVm> MonthlyRevenues { get; set; } = new();
}

public sealed class TopBookVm
{
    public string Title { get; set; } = string.Empty;

    public string Isbn { get; set; } = string.Empty;

    public int BorrowCount { get; set; }

    public int AvailableCopies { get; set; }

    public int TotalCopies { get; set; }

    public List<TopBookBorrowerVm> ActiveBorrowers { get; set; } = new();
}

public sealed class TopBookBorrowerVm
{
    public string MemberName { get; set; } = string.Empty;

    public int LoanId { get; set; }

    public string DueDate { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;
}

public sealed class TopMemberVm
{
    public string Name { get; set; } = string.Empty;

    public int LoanCount { get; set; }
}

public sealed class MemberBorrowReportRowVm
{
    public string Name { get; set; } = string.Empty;

    public int LoanCount { get; set; }

    public int TotalBorrowedItems { get; set; }

    public List<string> BorrowedTitles { get; set; } = new();

    public List<MemberLoanSnapshotVm> RecentLoans { get; set; } = new();
}

public sealed class MemberLoanSnapshotVm
{
    public int LoanId { get; set; }

    public string LoanDate { get; set; } = string.Empty;

    public string DueDate { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public int ItemCount { get; set; }

    public List<string> BookTitles { get; set; } = new();
}

public sealed class MonthlyRevenueVm
{
    public int Month { get; set; }

    public decimal TotalFineCollected { get; set; }

    public int PaymentCount { get; set; }

    public List<MonthlyFineDetailVm> FineDetails { get; set; } = new();
}

public sealed class MonthlyFineDetailVm
{
    public DateTime PaymentDate { get; set; }

    public string MemberName { get; set; } = string.Empty;

    public string MemberCode { get; set; } = string.Empty;

    public decimal AmountPaid { get; set; }

    public int? LoanId { get; set; }

    public List<string> BookTitles { get; set; } = new();
}
