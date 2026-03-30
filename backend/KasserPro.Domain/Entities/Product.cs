namespace KasserPro.Domain.Entities;

using KasserPro.Domain.Common;
using KasserPro.Domain.Enums;

public class Product : BaseEntity
{
    public int TenantId { get; set; }
    
    public string Name { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string? Description { get; set; }
    public string? Sku { get; set; }
    public string? Barcode { get; set; }
    public decimal Price { get; set; }
    public decimal? Cost { get; set; }
    public decimal? TaxRate { get; set; } // null = use branch default
    public bool TaxInclusive { get; set; } = true; // Egypt VAT is inclusive
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// Product type determines inventory behavior:
    /// - Physical: TrackInventory = true (automatic)
    /// - Service: TrackInventory = false (automatic)
    /// </summary>
    public ProductType Type { get; set; } = ProductType.Physical;
    
    /// <summary>
    /// Automatically set based on Type. Not user-controlled.
    /// Physical products always track inventory, Service products never do.
    /// </summary>
    public bool TrackInventory { get; set; } = true;
    
    /// <summary>
    /// Alert threshold - show warning when stock falls below this level
    /// </summary>
    public int? LowStockThreshold { get; set; }
    
    /// <summary>
    /// Suggested reorder level for inventory management
    /// </summary>
    public int? ReorderPoint { get; set; }
    
    /// <summary>
    /// Last time stock was updated (for audit trail)
    /// </summary>
    public DateTime? LastStockUpdate { get; set; }
    
    /// <summary>
    /// Average cost price (updated from purchase invoices)
    /// </summary>
    public decimal? AverageCost { get; set; }
    
    /// <summary>
    /// Last purchase price
    /// </summary>
    public decimal? LastPurchasePrice { get; set; }
    
    /// <summary>
    /// Date of last purchase
    /// </summary>
    public DateTime? LastPurchaseDate { get; set; }

    public int CategoryId { get; set; }
    
    // Navigation
    public Tenant Tenant { get; set; } = null!;
    public Category Category { get; set; } = null!;
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    public ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();
    public ICollection<SupplierProduct> SupplierProducts { get; set; } = new List<SupplierProduct>();
    public ICollection<PurchaseInvoiceItem> PurchaseInvoiceItems { get; set; } = new List<PurchaseInvoiceItem>();
    public ICollection<BranchInventory> BranchInventories { get; set; } = new List<BranchInventory>();
    public ICollection<BranchProductPrice> BranchPrices { get; set; } = new List<BranchProductPrice>();
    public ICollection<InventoryTransfer> InventoryTransfers { get; set; } = new List<InventoryTransfer>();
}
