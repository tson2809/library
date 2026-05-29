namespace Client_web.Models.ViewModels;

public sealed class LoanDetailVm
{
    public int LoanId { get; set; }

    public string Member { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string? Note { get; set; }

    public List<LoanDetailItemVm> Items { get; set; } = new();
}

public sealed class LoanDetailItemVm
{
    public int LoanItemId { get; set; }

    public string Barcode { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string CopyStatus { get; set; } = string.Empty;

    public string? PhysicalCondition { get; set; }

    public string? ReturnedAt { get; set; }
}
