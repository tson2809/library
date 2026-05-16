namespace Server.Interface;

public interface IMembersDataAccess
{
    Task<List<object>> GetMembersListAsync(CancellationToken cancellationToken);

    Task<int> CreateMemberAsync(string fullName, string? email, string? phone, string? addressLine, CancellationToken cancellationToken);

    Task UpdateMemberAsync(int memberId, string? email, string? phone, string? addressLine, bool? isActive, CancellationToken cancellationToken);

    Task<object?> GetMemberBorrowingStatusAsync(string memberCode, CancellationToken cancellationToken);

    Task<Server.Contracts.Members.MemberPortalSummaryDto?> VerifyMemberAccessAsync(string memberCode, string phoneOrEmail, CancellationToken cancellationToken);

    Task<Server.Contracts.Members.MemberStatementDto?> GetMemberStatementAsync(string memberCode, CancellationToken cancellationToken);
}
