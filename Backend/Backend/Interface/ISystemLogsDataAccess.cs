namespace Server.Interface;

public interface ISystemLogsDataAccess
{
    Task<List<object>> GetSystemLogsAsync(CancellationToken cancellationToken);
    Task AddSystemLogAsync(string actionType, string entityName, string? entityId, string? description, int? userId, CancellationToken cancellationToken);
}
