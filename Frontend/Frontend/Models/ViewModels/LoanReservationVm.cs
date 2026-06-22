namespace Client_web.Models.ViewModels;

public sealed class LoanReservationVm
{
    public int ReservationId { get; set; }

    public string RequestedAt { get; set; } = string.Empty;

    public string MemberCode { get; set; } = string.Empty;

    public string Member { get; set; } = string.Empty;

    public int BookId { get; set; }

    public string BookTitle { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string StatusText { get; set; } = string.Empty;

    public bool CanCancel { get; set; }

    public int QueuePosition { get; set; }

    public string? ReservedBarcode { get; set; }

    public string? Note { get; set; }
}
