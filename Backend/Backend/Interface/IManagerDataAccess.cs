namespace Server.Interface;

public interface IManagerDataAccess
{
    Task<object> GetManagerDashboardAsync(CancellationToken cancellationToken);

    Task<object> GetManagerRevenueAsync(int year, CancellationToken cancellationToken);

    Task<int> CollectFinePaymentAsync(string? memberCode, int? loanId, decimal amountPaid, string? paymentMethod, string? note, string receivedByUsername, CancellationToken cancellationToken);

    Task<object> GetFinePaymentHistoryAsync(string? memberKeyword, string? receivedByKeyword, string? exactReceivedByUsername, DateOnly? fromDate, DateOnly? toDate, int page, int pageSize, CancellationToken cancellationToken);
}
