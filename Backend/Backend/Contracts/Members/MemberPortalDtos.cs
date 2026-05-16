using System.ComponentModel.DataAnnotations;

namespace Server.Contracts.Members;

public sealed class VerifyMemberAccessRequest
{
    [Required(ErrorMessage = "Vui lòng nhập mã thành viên.")]
    [StringLength(20)]
    public string MemberCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập email hoặc số điện thoại.")]
    [StringLength(120)]
    public string PhoneOrEmail { get; set; } = string.Empty;
}

public sealed class MemberPortalSummaryDto
{
    public int MemberId { get; set; }

    public string MemberCode { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public bool IsActive { get; set; }
}

public sealed class MemberStatementDto
{
    public MemberPortalSummaryDto Member { get; set; } = new();

    public int BorrowingLoans { get; set; }

    public int OverdueLoans { get; set; }

    public decimal TotalFine { get; set; }

    public decimal TotalPaid { get; set; }

    public decimal OutstandingFine { get; set; }

    public bool LoanBlocked { get; set; }

    public List<MemberLoanHistoryDto> Loans { get; set; } = new();

    public List<MemberFinePaymentDto> FinePayments { get; set; } = new();

    public List<MemberReservationDto> Reservations { get; set; } = new();
}

public sealed class MemberLoanHistoryDto
{
    public int LoanId { get; set; }

    public DateOnly LoanDate { get; set; }

    public DateOnly DueDate { get; set; }

    public DateOnly? ReturnDate { get; set; }

    public string Status { get; set; } = string.Empty;

    public string StatusText { get; set; } = string.Empty;

    public string? Note { get; set; }

    public int RenewalCount { get; set; }

    public decimal TotalFine { get; set; }

    public List<MemberLoanItemDto> Items { get; set; } = new();
}

public sealed class MemberLoanItemDto
{
    public string Title { get; set; } = string.Empty;

    public string Barcode { get; set; } = string.Empty;

    public string? ConditionBefore { get; set; }

    public string? ConditionAfter { get; set; }

    public DateTime? ReturnedAt { get; set; }

    public decimal FineAmount { get; set; }
}

public sealed class MemberFinePaymentDto
{
    public int PaymentId { get; set; }

    public DateTime PaymentDate { get; set; }

    public decimal AmountPaid { get; set; }

    public string? PaymentMethod { get; set; }

    public string? Note { get; set; }

    public int? LoanId { get; set; }

    public string? ReceivedByName { get; set; }
}

public sealed class MemberReservationDto
{
    public int ReservationId { get; set; }

    public DateTime RequestedAt { get; set; }

    public string BookTitle { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string StatusText { get; set; } = string.Empty;

    public string? ReservedBarcode { get; set; }

    public DateTime? FulfilledAt { get; set; }

    public DateTime? CancelledAt { get; set; }

    public string? Note { get; set; }
}
