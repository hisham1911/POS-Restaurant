namespace KasserPro.Domain.Entities;

using KasserPro.Domain.Common;

public class OrderItem : BaseEntity
{
    /// <summary>
    /// Product ID - nullable to support custom POS items
    /// If null, this is a custom item (IsCustomItem = true)
    /// </summary>
    public int? ProductId { get; set; }

    /// <summary>
    /// Indicates if this is a custom POS item (not from product catalog)
    /// Custom items skip product validation and inventory tracking
    /// </summary>
    public bool IsCustomItem { get; set; } = false;

    /// <summary>
    /// Custom item name (used when IsCustomItem = true)
    /// </summary>
    public string? CustomName { get; set; }

    /// <summary>
    /// Custom item unit price (used when IsCustomItem = true)
    /// </summary>
    public decimal? CustomUnitPrice { get; set; }

    /// <summary>
    /// Custom item tax rate (used when IsCustomItem = true)
    /// </summary>
    public decimal? CustomTaxRate { get; set; }

    // Product Snapshot (immutable at order time)
    public string ProductName { get; set; } = string.Empty;
    public string? ProductNameEn { get; set; }
    public string? ProductSku { get; set; }
    public string? ProductBarcode { get; set; }

    // Price Snapshot
    public decimal UnitPrice { get; set; }
    public decimal? UnitCost { get; set; }
    public decimal OriginalPrice { get; set; } // Price before any discount

    public int Quantity { get; set; }

    /// <summary>
    /// Tracks how many units of this item have been refunded across partial refunds.
    /// Prevents the same item from being refunded more times than originally ordered.
    /// </summary>
    public int RefundedQuantity { get; set; } = 0;

    // Discount Snapshot
    public string? DiscountType { get; set; } // "percentage" or "fixed"
    public decimal? DiscountValue { get; set; }
    public decimal DiscountAmount { get; set; } = 0;
    public string? DiscountReason { get; set; }

    // Tax Snapshot
    public decimal TaxRate { get; set; } = 14;
    public decimal TaxAmount { get; set; }
    public bool TaxInclusive { get; set; } = false; // Tax Exclusive (Additive) — UnitPrice is NET

    public decimal Subtotal { get; set; }
    public decimal Total { get; set; }

    public string? Notes { get; set; }

    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;

    /// <summary>
    /// Navigation to Product - nullable for custom items
    /// </summary>
    public Product? Product { get; set; }
}
