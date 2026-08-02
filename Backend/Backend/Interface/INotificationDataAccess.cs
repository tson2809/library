namespace Server.Interface;

public interface INotificationDataAccess
{
    Task<int> CreateNotificationAsync(string actorUsername, string title, string content, bool sendToAll, IReadOnlyList<string> recipientUsernames, CancellationToken cancellationToken);

    Task<List<object>> GetNotificationsForUserAsync(string actorUsername, CancellationToken cancellationToken);

    Task<object?> GetNotificationDetailForUserAsync(string actorUsername, int notificationId, CancellationToken cancellationToken);
}
