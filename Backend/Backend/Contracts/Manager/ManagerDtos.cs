using System.ComponentModel.DataAnnotations;

namespace Server.Contracts.Manager;

public sealed class CollectFinePaymentRequest
{
    [StringLength(20)]
    public string? MemberCode { get; set; }

    public int? LoanId { get; set; }

    [Range(1, double.MaxValue, ErrorMessage = "Số tiền thu phải lớn hơn 0.")]
    public decimal AmountPaid { get; set; }

    [StringLength(50)]
    public string? PaymentMethod { get; set; }

    [StringLength(1000)]
    public string? Note { get; set; }
}
