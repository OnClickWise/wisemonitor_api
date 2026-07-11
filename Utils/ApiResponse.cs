namespace WiseMonitor.Api.Utils;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Message { get; set; }
    public PaginationMeta? Meta { get; set; }
}

public class PaginationMeta
{
    public int Page { get; set; }
    public int Limit { get; set; }
    public int Total { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)Total / Limit);
}

public class PagedResult<T>
{
    public IEnumerable<T> Items { get; set; } = [];
    public PaginationMeta Meta { get; set; } = new();
}
