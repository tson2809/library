namespace Client_web.Models.ViewModels;

public sealed class FinePaymentHistoryVm
{
    public int PaymentId { get; set; }

    public DateTime PaymentDate { get; set; }

    public decimal AmountPaid { get; set; }

    public string? PaymentMethod { get; set; }

    public string? Note { get; set; }

    public int? LoanId { get; set; }

    public string MemberCode { get; set; } = string.Empty;

    public string MemberName { get; set; } = string.Empty;

    public string? ReceivedByUsername { get; set; }

    public string? ReceivedByName { get; set; }
}
