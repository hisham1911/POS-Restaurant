namespace KasserPro.Domain.Entities;

using System.ComponentModel.DataAnnotations;
using KasserPro.Domain.Common;
using KasserPro.Domain.Enums;

/// <summary>
/// Represents a batch/lot of products received in inventory.
/// Enables FEFO (First Expired First Out) stock management.
/// </summary>
public class ProductBatch : BaseEntity
{
    public int TenantId { get; set; }
    public int BranchId { get; set; }
    public int ProductId { get; set; }

    /// <summary>
    /// Batch number / lot number (e.g., "LOT-2026-001")
    /// </summary>
    [MaxLength(100)]
    public string? BatchNumber { get; set; }

    /// <summary>
    /// Date of production (optional)
    /// </summary>
    public DateTime? ProductionDate { get; set; }

    /// <summary>
    /// Expiry date — critical for FEFO
    /// </summary>
    public DateTime? ExpiryDate { get; set; }

    /// <summary>
    /// Date the batch was received (purchase date)
    /// </summary>
    public DateTime PurchaseDate { get; set; }

    /// <summary>
    /// Optional: reference to purchase invoice
    /// </summary>
    public int? PurchaseInvoiceId { get; set; }

    /// <summary>
    /// Quantity remaining in this batch
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// Initial quantity when batch was received
    /// </summary>
    public int InitialQuantity { get; set; }

    /// <summary>
    /// Cost price per unit at time of purchase
    /// </summary>
    public decimal? CostPrice { get; set; }

    /// <summary>
    /// Optional selling price specific to this batch (overrides Product.Price)
    /// </summary>
    public decimal? SellingPrice { get; set; }

    /// <summary>
    /// Supplier name snapshot
    /// </summary>
    [MaxLength(200)]
    public string? SupplierName { get; set; }

    /// <summary>
    /// Batch status: Active, Expired, Depleted
    /// </summary>
    public BatchStatus Status { get; set; } = BatchStatus.Active;

    /// <summary>
    /// Notes about this batch
    /// </summary>
    [MaxLength(500)]
    public string? Notes { get; set; }

    /// <summary>
    /// When this batch status was last updated
    /// </summary>
    public DateTime? StatusUpdatedAt { get; set; }

    // Navigation
    public Tenant Tenant { get; set; } = null!;
    public Branch Branch { get; set; } = null!;
    public Product Product { get; set; } = null!;
    public PurchaseInvoice? PurchaseInvoice { get; set; }
    public ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();
}
