namespace KasserPro.Domain.Entities;

using KasserPro.Domain.Common;

/// <summary>
/// Represents inventory for a specific product in a specific branch.
/// This replaces the global Product.StockQuantity with branch-specific inventory.
/// </summary>
public class BranchInventory : BaseEntity
{
    /// <summary>
    /// Tenant ID for multi-tenancy
    /// </summary>
    public int TenantId { get; set; }
    
    /// <summary>
    /// Branch ID - which branch this inventory belongs to
    /// </summary>
    public int BranchId { get; set; }
    
    /// <summary>
    /// Product ID - which product this inventory is for
    /// </summary>
    public int ProductId { get; set; }
    
    /// <summary>
    /// Current quantity available in this branch
    /// </summary>
    public decimal Quantity { get; set; }
    
    /// <summary>
    /// Reorder level - when quantity falls below this, alert is triggered
    /// Each branch can have different reorder levels based on demand
    /// </summary>
    public int ReorderLevel { get; set; }
    
    /// <summary>
    /// Last time this inventory was updated (sale, purchase, transfer, adjustment)
    /// </summary>
    public DateTime LastUpdatedAt { get; set; }
    
    // Navigation Properties
    public Tenant Tenant { get; set; } = null!;
    public Branch Branch { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
