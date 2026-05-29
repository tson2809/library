namespace Client_web.Models.ViewModels;

public sealed class LoanVm
{
    public int LoanId { get; set; }

    public string Member { get; set; } = string.Empty;

    public string ProcessedBy { get; set; } = string.Empty;

    public string LoanDate { get; set; } = string.Empty;

    public string DueDate { get; set; } = string.Empty;

    public string? ReturnDate { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? StatusText { get; set; }

    public int ItemCount { get; set; }

    public int RenewalCount { get; set; }

    public List<string> BookTitles { get; set; } = new();
}
