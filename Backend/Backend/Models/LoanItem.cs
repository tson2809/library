using System;
using System.Collections.Generic;

namespace Server.Models;

public partial class LoanItem
{
    public int LoanItemId { get; set; }

    public int LoanId { get; set; }

    public int BookCopyId { get; set; }

    public string? ConditionBefore { get; set; }

    public string? ConditionAfter { get; set; }

    public DateTime? ReturnedAt { get; set; }

    public decimal FineAmount { get; set; }

    public virtual BookCopy BookCopy { get; set; } = null!;

    public virtual Loan Loan { get; set; } = null!;
}
