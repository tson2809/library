using System;

namespace Server.Models;

public partial class BookReservation
{
    public int ReservationId { get; set; }

    public int MemberId { get; set; }

    public int BookId { get; set; }

    public int? ReservedCopyId { get; set; }

    public int CreatedByUserId { get; set; }

    public int? FulfilledByUserId { get; set; }

    public DateTime RequestedAt { get; set; }

    public DateTime? FulfilledAt { get; set; }

    public DateTime? CancelledAt { get; set; }

    public string Status { get; set; } = null!;

    public string? Note { get; set; }

    public virtual Book Book { get; set; } = null!;

    public virtual User CreatedByUser { get; set; } = null!;

    public virtual User? FulfilledByUser { get; set; }

    public virtual Member Member { get; set; } = null!;

    public virtual BookCopy? ReservedCopy { get; set; }
}