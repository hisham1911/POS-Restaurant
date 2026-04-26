namespace KasserPro.Application.DTOs.Common;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? ErrorCode { get; set; }
    public T? Data { get; set; }
    public List<string>? Errors { get; set; }

    public static ApiResponse<T> Ok(T data, string? message = null) => new()
    {
        Success = true,
        Message = message,
        Data = data
    };

    public static ApiResponse<T> Fail(string message, List<string>? errors = null) => new()
    {
        Success = false,
        Message = message,
        Errors = errors
    };

    public static ApiResponse<T> Fail(string errorCode, string message, List<string>? errors = null) => new()
    {
        Success = false,
        ErrorCode = errorCode,
        Message = message,
        Errors = errors
    };
}


public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal TotalSpentAmount { get; set; }
    public decimal TotalDueAmount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
    
    public PagedResult() { }
    
    public PagedResult(
        List<T> items,
        int totalCount,
        int page,
        int pageSize,
        decimal totalAmount = 0m,
        decimal totalSpentAmount = 0m,
        decimal totalDueAmount = 0m)
    {
        Items = items;
        TotalCount = totalCount;
        TotalAmount = totalAmount;
        TotalSpentAmount = totalSpentAmount;
        TotalDueAmount = totalDueAmount;
        Page = page;
        PageSize = pageSize;
    }
}
