namespace KasserPro.Application.DTOs.Products;

using KasserPro.Domain.Enums;

public class ProductDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string? Description { get; set; }
    public string? Sku { get; set; }
    public string? Barcode { get; set; }
    public decimal Price { get; set; }
    public decimal? Cost { get; set; }
    public decimal? TaxRate { get; set; }
    public bool TaxInclusive { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; }
    
    /// <summary>
    /// Product type (Physical or Service)
    /// </summary>
    public ProductType Type { get; set; }
    
    /// <summary>
    /// Automatically determined by Type
    /// </summary>
    public bool TrackInventory { get; set; }
    
    /// <summary>
    /// Current stock quantity for the current branch (from BranchInventories table)
    /// </summary>
    public int? CurrentBranchStock { get; set; }
    
    public int CategoryId { get; set; }
    public string? CategoryName { get; set; }
    
    // Sellable V1: Inventory management fields
    public int? LowStockThreshold { get; set; }
    public int? ReorderPoint { get; set; }
    public DateTime? LastStockUpdate { get; set; }
    
    /// <summary>
    /// Indicates if product is below low stock threshold
    /// </summary>
    public bool IsLowStock => TrackInventory && LowStockThreshold.HasValue && CurrentBranchStock < LowStockThreshold;
}
