namespace KasserPro.Domain.Entities;

using System.ComponentModel.DataAnnotations;
using KasserPro.Domain.Common;
using KasserPro.Domain.Enums;

/// <summary>
/// Tracks all stock movements for complete inventory audit trail.
/// Every stock change (sale, refund, adjustment) creates a record.
/// </summary>
public class StockMovement : BaseEntity
{
    public int TenantId { get; set; }
    public int BranchId { get; set; }
    public int ProductId { get; set; }
    
    /// <summary>
    /// Type of movement (Sale, Refund, Adjustment, etc.)
    /// </summary>
    public StockMovementType Type { get; set; }
    
    /// <summary>
    /// Quantity changed. Positive = stock increase, Negative = stock decrease
    /// </summary>
    public int Quantity { get; set; }
    
    /// <summary>
    /// Reference to the source entity (OrderId, RefundLogId, etc.)
    /// </summary>
    public int? ReferenceId { get; set; }
    
    /// <summary>
    /// Type of reference entity ("Order", "Refund", "Adjustment")
    /// </summary>
    [MaxLength(50)]
    public string? ReferenceType { get; set; }
    
    /// <summary>
    /// Stock quantity before this movement
    /// </summary>
    public int BalanceBefore { get; set; }
    
    /// <summary>
    /// Stock quantity after this movement
    /// </summary>
    public int BalanceAfter { get; set; }
    
    /// <summary>
    /// Reason for the movement (required for adjustments)
    /// </summary>
    [MaxLength(500)]
    public string? Reason { get; set; }
    
    /// <summary>
    /// User who created this movement
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// Optional batch reference for batch-level stock tracking (FEFO)
    /// </summary>
    public int? BatchId { get; set; }

    // Navigation
    public Tenant Tenant { get; set; } = null!;
    public Branch Branch { get; set; } = null!;
    public Product Product { get; set; } = null!;
    public User User { get; set; } = null!;
    public ProductBatch? Batch { get; set; }
}
