namespace Server.Interface;

public interface IReportsDataAccess
{
    Task<List<object>> GetOverdueReportAsync(CancellationToken cancellationToken);
}
