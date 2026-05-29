using System;
using System.Collections.Generic;

namespace Server.Models;

public partial class Loan
{
    public int LoanId { get; set; }

    public int MemberId { get; set; }

    public int ProcessedByUserId { get; set; }

    public DateOnly LoanDate { get; set; }

    public DateOnly DueDate { get; set; }

    public DateOnly? ReturnDate { get; set; }

    public string Status { get; set; } = null!;

    public string? Note { get; set; }

    public int RenewalCount { get; set; }

    public virtual ICollection<FinePayment> FinePayments { get; set; } = new List<FinePayment>();

    public virtual ICollection<LoanItem> LoanItems { get; set; } = new List<LoanItem>();

    public virtual Member Member { get; set; } = null!;

    public virtual User ProcessedByUser { get; set; } = null!;
}
