namespace Server.Interface;

public interface ILibraryDataAccess :
    IAuthDataAccess,
    IBooksDataAccess,
    IMembersDataAccess,
    ILoansDataAccess,
    IManagerDataAccess,
    ISystemLogsDataAccess,
    IReportsDataAccess,
    INotificationDataAccess
{
}
