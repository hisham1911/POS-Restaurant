namespace KasserPro.Application.DTOs.ProductBatches;

public class ProductBatchDto
{
    public int Id { get; set; }
    public string? BatchNumber { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal InitialQuantity { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public DateTime PurchaseDate { get; set; }
    public DateTime? ProductionDate { get; set; }
    public decimal? CostPrice { get; set; }
    public decimal? SellingPrice { get; set; }
    public string? SupplierName { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public int? DaysUntilExpiry { get; set; }
    public int BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public bool IsRecommended { get; set; }
}

public class ProductBatchListDto
{
    public int Id { get; set; }
    public string? BatchNumber { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class CreateProductBatchDto
{
    public int ProductId { get; set; }
    public string? BatchNumber { get; set; }
    public decimal Quantity { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public DateTime? ProductionDate { get; set; }
    public decimal? CostPrice { get; set; }
    public decimal? SellingPrice { get; set; }
    public string? SupplierName { get; set; }
    public string? Notes { get; set; }
}

public class UpdateProductBatchDto
{
    public string? BatchNumber { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public DateTime? ProductionDate { get; set; }
    public decimal? SellingPrice { get; set; }
    public string? Notes { get; set; }
}

public class HoldBatchRequest
{
    public string Reason { get; set; } = string.Empty;
}

public class BatchExpiryAlertDto
{
    public int Id { get; set; }
    public string? BatchNumber { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public int? DaysUntilExpiry { get; set; }
    public string AlertLevel { get; set; } = string.Empty;
}

public class BatchExpirySummaryDto
{
    public int TotalBatches { get; set; }
    public int ExpiredBatches { get; set; }
    public int NearExpiryBatches { get; set; }
    public List<BatchExpiryAlertDto> Alerts { get; set; } = new();
}
