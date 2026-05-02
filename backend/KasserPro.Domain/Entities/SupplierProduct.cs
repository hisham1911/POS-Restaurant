namespace KasserPro.Domain.Entities;

using KasserPro.Domain.Common;

/// <summary>
/// Represents the many-to-many relationship between suppliers and products
/// </summary>
public class SupplierProduct : BaseEntity
{
    public int TenantId { get; set; }
    public int SupplierId { get; set; }
    public int ProductId { get; set; }
    
    /// <summary>
    /// Is this the preferred supplier for this product?
    /// </summary>
    public bool IsPreferred { get; set; } = false;
    
    /// <summary>
    /// Last purchase price from this supplier
    /// </summary>
    public decimal? LastPurchasePrice { get; set; }
    
    /// <summary>
    /// Date of last purchase from this supplier
    /// </summary>
    public DateTime? LastPurchaseDate { get; set; }
    
    /// <summary>
    /// Total quantity purchased from this supplier (lifetime)
    /// </summary>
    public int TotalQuantityPurchased { get; set; } = 0;
    
    /// <summary>
    /// Total amount spent with this supplier (lifetime)
    /// </summary>
    public decimal TotalAmountSpent { get; set; } = 0;
    
    public string? Notes { get; set; }
    
    // Navigation properties
    public Supplier Supplier { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
