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
    public bool TaxInclusive { get; set; } = true;
    
    /// <summary>
    /// Product type determines inventory behavior:
    /// - Physical: Inventory tracking enabled automatically
    /// - Service: Inventory tracking disabled automatically
    /// </summary>
    public ProductType Type { get; set; } = ProductType.Physical;
    
    // Inventory fields (only used for Physical products)
    /// <summary>
    /// Stock quantity for the current branch (updates BranchInventories table)
    /// </summary>
    public int CurrentBranchStock { get; set; } = 0;
    public int LowStockThreshold { get; set; } = 5;
    public int? ReorderPoint { get; set; }
}
