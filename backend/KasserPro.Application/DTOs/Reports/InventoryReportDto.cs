namespace KasserPro.Application.DTOs.Reports;

/// <summary>
/// Inventory per branch report
/// </summary>
public class BranchInventoryReportDto
{
    public int BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public int TotalProducts { get; set; }
    public decimal TotalQuantity { get; set; }
    public int LowStockCount { get; set; }
    public decimal TotalValue { get; set; } // Quantity * AverageCost
    public List<BranchInventoryItemDto> Items { get; set; } = new();
}

public class BranchInventoryItemDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ProductSku { get; set; }
    public string? CategoryName { get; set; }
    public decimal Quantity { get; set; }
    public int ReorderLevel { get; set; }
    public bool IsLowStock { get; set; }
    public decimal? AverageCost { get; set; }
    public decimal? TotalValue { get; set; }
    public DateTime LastUpdatedAt { get; set; }
}

/// <summary>
/// Unified inventory view across all branches
/// </summary>
public class UnifiedInventoryReportDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ProductSku { get; set; }
    public string? CategoryName { get; set; }
    public decimal TotalQuantity { get; set; }
    public decimal? AverageCost { get; set; }
    public decimal? TotalValue { get; set; }
    public int BranchCount { get; set; }
    public int LowStockBranchCount { get; set; }
    public List<BranchStockDto> BranchStocks { get; set; } = new();
}

public class BranchStockDto
{
    public int BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public int ReorderLevel { get; set; }
    public bool IsLowStock { get; set; }
}

/// <summary>
/// Transfer history report
/// </summary>
public class TransferHistoryReportDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int TotalTransfers { get; set; }
    public int CompletedTransfers { get; set; }
    public int PendingTransfers { get; set; }
    public int CancelledTransfers { get; set; }
    public decimal TotalQuantityTransferred { get; set; }
    public List<TransferSummaryDto> Transfers { get; set; } = new();
    public List<BranchTransferStatsDto> BranchStats { get; set; } = new();
}

public class TransferSummaryDto
{
    public int Id { get; set; }
    public string TransferNumber { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string FromBranchName { get; set; } = string.Empty;
    public string ToBranchName { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime? CompletedAt { get; set; }
}

public class BranchTransferStatsDto
{
    public int BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public int TransfersSent { get; set; }
    public int TransfersReceived { get; set; }
    public decimal QuantitySent { get; set; }
    public decimal QuantityReceived { get; set; }
    public decimal NetChange { get; set; } // QuantityReceived - QuantitySent
}

/// <summary>
/// Low stock summary report
/// </summary>
public class LowStockSummaryReportDto
{
    public int TotalLowStockItems { get; set; }
    public int AffectedBranches { get; set; }
    public int CriticalItems { get; set; } // Quantity = 0
    public decimal EstimatedRestockValue { get; set; }
    public List<LowStockItemDto> Items { get; set; } = new();
    public List<BranchLowStockStatsDto> BranchStats { get; set; } = new();
}

public class LowStockItemDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ProductSku { get; set; }
    public string? CategoryName { get; set; }
    public decimal TotalQuantity { get; set; }
    public int TotalReorderLevel { get; set; }
    public decimal Shortage { get; set; }
    public decimal? AverageCost { get; set; }
    public decimal? EstimatedRestockCost { get; set; }
    public List<BranchLowStockDetailDto> BranchDetails { get; set; } = new();
}

public class BranchLowStockDetailDto
{
    public int BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public int ReorderLevel { get; set; }
    public decimal Shortage { get; set; }
    public bool IsCritical { get; set; } // Quantity = 0
}

public class BranchLowStockStatsDto
{
    public int BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public int LowStockCount { get; set; }
    public int CriticalCount { get; set; }
    public decimal EstimatedRestockValue { get; set; }
}

/// <summary>
/// Query parameters for reports
/// </summary>
public class InventoryReportQueryParams
{
    public int? BranchId { get; set; }
    public int? CategoryId { get; set; }
    public bool? LowStockOnly { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}
