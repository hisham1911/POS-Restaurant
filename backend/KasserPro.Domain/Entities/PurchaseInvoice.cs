namespace KasserPro.Domain.Entities;

using KasserPro.Domain.Common;
using KasserPro.Domain.Enums;

/// <summary>
/// Represents a purchase invoice from a supplier
/// </summary>
public class PurchaseInvoice : BaseEntity
{
    public int TenantId { get; set; }
    public int BranchId { get; set; }
    
    /// <summary>
    /// Auto-generated invoice number (e.g., PI-2026-0001)
    /// </summary>
    public string InvoiceNumber { get; set; } = string.Empty;
    
    public int SupplierId { get; set; }
    
    /// <summary>
    /// Supplier snapshot at invoice time
    /// </summary>
    public string SupplierName { get; set; } = string.Empty;
    public string? SupplierPhone { get; set; }
    public string? SupplierAddress { get; set; }
    
    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;
    public PurchaseInvoiceStatus Status { get; set; } = PurchaseInvoiceStatus.Draft;
    
    /// <summary>
    /// Sum of all items (before tax)
    /// </summary>
    public decimal Subtotal { get; set; }
    
    /// <summary>
    /// Tax rate snapshot (from Tenant settings at invoice time)
    /// </summary>
    public decimal TaxRate { get; set; }
    
    /// <summary>
    /// Whether tax is enabled for this invoice
    /// </summary>
    public bool IsTaxEnabled { get; set; } = true;
    
    /// <summary>
    /// Calculated tax amount (Subtotal * TaxRate / 100)
    /// </summary>
    public decimal TaxAmount { get; set; }
    
    /// <summary>
    /// Total = Subtotal + TaxAmount
    /// </summary>
    public decimal Total { get; set; }
    
    /// <summary>
    /// Sum of all payments
    /// </summary>
    public decimal AmountPaid { get; set; } = 0;
    
    /// <summary>
    /// Remaining amount (Total - AmountPaid)
    /// </summary>
    public decimal AmountDue { get; set; }
    
    public string? Notes { get; set; }
    
    /// <summary>
    /// User who created the invoice
    /// </summary>
    public int CreatedByUserId { get; set; }
    public string? CreatedByUserName { get; set; }
    
    /// <summary>
    /// User who confirmed the invoice (Admin only)
    /// </summary>
    public int? ConfirmedByUserId { get; set; }
    public string? ConfirmedByUserName { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    
    /// <summary>
    /// Cancellation details
    /// </summary>
    public int? CancelledByUserId { get; set; }
    public string? CancelledByUserName { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    
    /// <summary>
    /// Whether inventory was adjusted when cancelled
    /// </summary>
    public bool InventoryAdjustedOnCancellation { get; set; } = false;
    
    // Navigation properties
    public Tenant Tenant { get; set; } = null!;
    public Branch Branch { get; set; } = null!;
    public Supplier Supplier { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
    public User? ConfirmedByUser { get; set; }
    public ICollection<PurchaseInvoiceItem> Items { get; set; } = new List<PurchaseInvoiceItem>();
    public ICollection<PurchaseInvoicePayment> Payments { get; set; } = new List<PurchaseInvoicePayment>();
}
