namespace KasserPro.Application.DTOs.Inventory;

using KasserPro.Domain.Enums;

/// <summary>
/// Stock taking (physical inventory count) session DTO
/// </summary>
public class StockTakingDto
{
    public int Id { get; set; }
    public string StockTakingNumber { get; set; } = string.Empty;
    public StockTakingStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public string? CreatedByUserName { get; set; }
    public int? CompletedByUserId { get; set; }
    public string? CompletedByUserName { get; set; }
    public string? Notes { get; set; }
    public int ItemCount { get; set; }
    public int TotalDifference { get; set; }
    public List<StockTakingItemDto> Items { get; set; } = new();
}

/// <summary>
/// Stock taking item line DTO
/// </summary>
public class StockTakingItemDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ProductSku { get; set; }
    public int SystemQuantity { get; set; }
    public int ActualQuantity { get; set; }
    public int Difference { get; set; }
    public string? Reason { get; set; }
    public int? BatchId { get; set; }
    public string? BatchNumber { get; set; }
}

/// <summary>
/// Request to create a new stock taking session
/// </summary>
public class CreateStockTakingRequest
{
    public string? Notes { get; set; }
}

/// <summary>
/// Request to add/update a stock taking item line
/// </summary>
public class UpsertStockTakingItemRequest
{
    public int ProductId { get; set; }
    public int ActualQuantity { get; set; }
    public string? Reason { get; set; }
    public int? BatchId { get; set; }
}

/// <summary>
/// Request to complete a stock taking and apply differences to inventory
/// </summary>
public class CompleteStockTakingRequest
{
    public bool ApplyAdjustments { get; set; } = true;
    public string? Notes { get; set; }
}
