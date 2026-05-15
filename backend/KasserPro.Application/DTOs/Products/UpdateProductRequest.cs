namespace KasserPro.Application.DTOs.Products;

using KasserPro.Domain.Enums;

public class UpdateProductRequest
{
    public string Name { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string? Description { get; set; }
    public string? Sku { get; set; }
    public string? Barcode { get; set; }
    public decimal Price { get; set; }
    public decimal? Cost { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; }
    public int CategoryId { get; set; }
    
    // Tax settings
    public decimal? TaxRate { get; set; }
    // Legacy compatibility field. Inclusive pricing is disabled and this value is ignored.
    public bool TaxInclusive { get; set; } = false;
    
    /// <summary>
    /// Product type determines inventory behavior:
    /// - Physical: Inventory tracking enabled automatically
    /// - Service: Inventory tracking disabled automatically
    /// </summary>
    public ProductType Type { get; set; } = ProductType.Physical;
    public UnitOfMeasure Unit { get; set; } = UnitOfMeasure.Piece;

    // Inventory fields (only used for Physical products)
    /// <summary>
    /// Stock quantity for the current branch (updates BranchInventories table)
    /// </summary>
    public decimal CurrentBranchStock { get; set; } = 0;
    public int LowStockThreshold { get; set; } = 5;
    public int? ReorderPoint { get; set; }
    
    /// <summary>
    /// Whether this product tracks batches (FEFO, expiry, cost-per-batch).
    /// Default true for all products.
    /// </summary>
    public bool IsBatchTracked { get; set; } = true;
}
