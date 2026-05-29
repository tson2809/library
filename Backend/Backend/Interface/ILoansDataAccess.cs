namespace Server.Interface;

public interface ILoansDataAccess
{
    Task<List<object>> GetLoansListAsync(CancellationToken cancellationToken);

    Task<object?> GetLoanDetailAsync(int loanId, CancellationToken cancellationToken);

    Task<int> CreateLoanAsync(string memberCode, string processedByUsername, DateOnly dueDate, IReadOnlyList<string> barcodes, string? conditionBefore, string? note, CancellationToken cancellationToken);

    Task<Server.Contracts.Loans.ReturnByBarcodeResultDto> ReturnBookByBarcodeAsync(string barcode, string? conditionAfter, CancellationToken cancellationToken);

    Task<List<object>> GetPendingReservationsAsync(CancellationToken cancellationToken);

    Task<List<Server.Contracts.Loans.ReservationDto>> GetReservationsAsync(string? status, CancellationToken cancellationToken);

    Task<int> CreateReservationAsync(string memberCode, int bookId, string actorUsername, string? note, CancellationToken cancellationToken);

    Task<Server.Contracts.Loans.ReservationActionResultDto> CancelReservationAsync(int reservationId, string actorUsername, CancellationToken cancellationToken);

    Task RenewLoanAsync(int loanId, DateOnly newDueDate, CancellationToken cancellationToken);

    Task<Server.Contracts.Loans.RenewLoanResultDto> RenewLoanWithResultAsync(int loanId, DateOnly newDueDate, CancellationToken cancellationToken);
}
