using System;
using System.Collections.Generic;

namespace Server.Models;

public partial class BookCopy
{
    public int BookCopyId { get; set; }

    public int BookId { get; set; }

    public string Barcode { get; set; } = null!;

    public DateOnly? AcquiredDate { get; set; }

    public string CopyStatus { get; set; } = null!;

    public string PhysicalCondition { get; set; } = null!;

    public string? LocationCode { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Book Book { get; set; } = null!;

    public virtual ICollection<LoanItem> LoanItems { get; set; } = new List<LoanItem>();

    public virtual ICollection<BookReservation> BookReservations { get; set; } = new List<BookReservation>();
}
