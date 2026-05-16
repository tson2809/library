using System;
using System.Collections.Generic;

namespace Server.Models;

public partial class Member
{
    public int MemberId { get; set; }

    public string MemberCode { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public DateOnly? DateOfBirth { get; set; }

    public string? Gender { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? AddressLine { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<FinePayment> FinePayments { get; set; } = new List<FinePayment>();

    public virtual ICollection<BookReservation> BookReservations { get; set; } = new List<BookReservation>();

    public virtual ICollection<Loan> Loans { get; set; } = new List<Loan>();
}
