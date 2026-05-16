namespace Client_web.Models.ViewModels;

public sealed class MemberVm
{
    public int MemberId { get; set; }

    public string MemberCode { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public bool IsActive { get; set; }

    public int BorrowingLoans { get; set; }

    public int OverdueLoans { get; set; }
}

