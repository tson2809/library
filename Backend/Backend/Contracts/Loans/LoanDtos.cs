using System.ComponentModel.DataAnnotations;

namespace Server.Contracts.Loans;

public sealed class CreateLoanRequest
{
    [Required(ErrorMessage = "Vui lòng nhập mã thành viên.")]
    [StringLength(20)]
    public string MemberCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập hạn trả.")]
    public DateOnly DueDate { get; set; }

    [MinLength(1, ErrorMessage = "Vui lòng chọn ít nhất một mã vạch.")]
    public List<string> Barcodes { get; set; } = new();

    [StringLength(30)]
    public string? ConditionBefore { get; set; }

    [StringLength(1000)]
    public string? Note { get; set; }
}

public sealed class ReturnByBarcodeRequest
{
    [Required(ErrorMessage = "Thiếu mã vạch.")]
    [StringLength(50)]
    public string Barcode { get; set; } = string.Empty;

    [StringLength(30)]
    public string? ConditionAfter { get; set; }
}

public sealed class RenewLoanRequest
{
    [Required(ErrorMessage = "Thiếu ngày hạn trả mới.")]
    public DateOnly NewDueDate { get; set; }
}

public sealed class CreateReservationRequest
{
    [Required(ErrorMessage = "Vui lòng nhập mã thành viên.")]
    [StringLength(20)]
    public string MemberCode { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn đầu sách cần đặt trước.")]
    public int BookId { get; set; }

    [StringLength(1000)]
    public string? Note { get; set; }
}

public sealed class CreateLoanResultDto
{
    public int LoanId { get; set; }
}

public sealed class ReturnByBarcodeResultDto
{
    public int LoanId { get; set; }

    public string? ReservedForMemberCode { get; set; }

    public string? ReservedForMemberName { get; set; }

    public string? ReservedBookTitle { get; set; }

    public string? ReservedBarcode { get; set; }

    public string? ReservationStatus { get; set; }
}

public sealed class RenewLoanResultDto
{
    public int LoanId { get; set; }

    public DateOnly OldDueDate { get; set; }

    public DateOnly NewDueDate { get; set; }

    public int RenewalCount { get; set; }

    public string Status { get; set; } = string.Empty;
}

public sealed class ReservationDto
{
    public int ReservationId { get; set; }

    public DateTime RequestedAt { get; set; }

    public string? Note { get; set; }

    public string MemberCode { get; set; } = string.Empty;

    public string Member { get; set; } = string.Empty;

    public int BookId { get; set; }

    public string BookTitle { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string StatusText { get; set; } = string.Empty;

    public string? ReservedBarcode { get; set; }

    public int QueuePosition { get; set; }

    public bool CanCancel { get; set; }
}

public sealed class ReservationActionResultDto
{
    public int ReservationId { get; set; }

    public string Status { get; set; } = string.Empty;

    public string StatusText { get; set; } = string.Empty;

    public string? ReleasedBarcode { get; set; }

    public int? ReassignedReservationId { get; set; }

    public string? ReassignedMemberCode { get; set; }

    public string? ReassignedBarcode { get; set; }
}
