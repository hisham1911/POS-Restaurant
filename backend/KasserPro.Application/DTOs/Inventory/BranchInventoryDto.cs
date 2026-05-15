namespace KasserPro.Application.DTOs.Inventory;

public class BranchInventoryDto
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ProductSku { get; set; }
    public string? ProductBarcode { get; set; }
    public decimal Quantity { get; set; }
    public decimal? BatchAvailableQuantity { get; set; }
    public int ReorderLevel { get; set; }
    public bool IsLowStock { get; set; }
    public DateTime LastUpdatedAt { get; set; }
    
    /// <summary>
    /// Whether this product tracks batches (FEFO, expiry, cost-per-batch).
    /// </summary>
    public bool IsBatchTracked { get; set; } = true;
}

public class BranchInventorySummaryDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ProductSku { get; set; }
    public decimal TotalQuantity { get; set; }
    public List<BranchInventoryDto> BranchInventories { get; set; } = new();
}
