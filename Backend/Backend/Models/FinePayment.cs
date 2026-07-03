using System;
using System.Collections.Generic;

namespace Server.Models;

public partial class FinePayment
{
    public int PaymentId { get; set; }

    public int MemberId { get; set; }

    public int? LoanId { get; set; }

    public decimal AmountPaid { get; set; }

    public DateTime PaymentDate { get; set; }

    public string? PaymentMethod { get; set; }

    public int? ReceivedByUserId { get; set; }

    public string? Note { get; set; }

    public virtual Loan? Loan { get; set; }

    public virtual Member Member { get; set; } = null!;

    public virtual User? ReceivedByUser { get; set; }
}
