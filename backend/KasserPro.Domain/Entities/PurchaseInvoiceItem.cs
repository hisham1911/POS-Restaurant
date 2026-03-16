namespace KasserPro.Domain.Entities;

using KasserPro.Domain.Common;

/// <summary>
/// Represents an item in a purchase invoice
/// </summary>
public class PurchaseInvoiceItem : BaseEntity
{
    public int PurchaseInvoiceId { get; set; }
    public int ProductId { get; set; }
    
    /// <summary>
    /// Product snapshot at invoice time
    /// </summary>
    public string ProductName { get; set; } = string.Empty;
    public string? ProductNameEn { get; set; }
    public string? ProductSku { get; set; }
    public string? ProductBarcode { get; set; }
    
    public int Quantity { get; set; }
    
    /// <summary>
    /// Purchase price per unit (cost price, not selling price)
    /// </summary>
    public decimal PurchasePrice { get; set; }
    
    /// <summary>
    /// Selling price per unit (retail price)
    /// </summary>
    public decimal SellingPrice { get; set; }
    
    /// <summary>
    /// Total for this item (Quantity * PurchasePrice)
    /// </summary>
    public decimal Total { get; set; }
    
    public string? Notes { get; set; }
    
    // Navigation properties
    public PurchaseInvoice PurchaseInvoice { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
