namespace KasserPro.Application.DTOs.Products;

using KasserPro.Domain.Enums;

public class CreateProductRequest
{
    public string Name { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string? Description { get; set; }
    public string? Sku { get; set; }
    public string? Barcode { get; set; }
    public decimal Price { get; set; }
    public decimal? Cost { get; set; }
    public string? ImageUrl { get; set; }
    public int CategoryId { get; set; }
    
    // Tax settings
    public decimal? TaxRate { get; set; } // null = use branch default
    public bool TaxInclusive { get; set; } = true;
    
    /// <summary>
    /// Product type determines inventory behavior:
    /// - Physical: Inventory tracking enabled automatically
    /// - Service: Inventory tracking disabled automatically
    /// </summary>
    public ProductType Type { get; set; } = ProductType.Physical;
    
    // Inventory fields (only used for Physical products)
    /// <summary>
    /// Initial stock quantity for the current branch (stored in BranchInventories)
    /// </summary>
    public int InitialBranchStock { get; set; } = 0;
    public int LowStockThreshold { get; set; } = 5;
    public int? ReorderPoint { get; set; }
    
    // Branch-specific initial stock (optional)
    // Key: BranchId, Value: Initial Quantity
    public Dictionary<int, int>? BranchStockQuantities { get; set; }
}
