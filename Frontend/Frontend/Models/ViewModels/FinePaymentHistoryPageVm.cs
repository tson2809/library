namespace Client_web.Models.ViewModels;

public sealed class FinePaymentHistoryPageVm
{
    public List<FinePaymentHistoryVm> Items { get; set; } = new();

    public PaginationVm Pagination { get; set; } = new();
}

public sealed class PaginationVm
{
    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages { get; set; }
}
