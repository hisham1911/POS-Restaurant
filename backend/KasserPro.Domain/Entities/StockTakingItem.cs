namespace KasserPro.Domain.Entities;

using System.ComponentModel.DataAnnotations;
using KasserPro.Domain.Common;

/// <summary>
/// Individual product count line within a stock taking session.
/// </summary>
public class StockTakingItem : BaseEntity
{
    public int StockTakingId { get; set; }
    public int ProductId { get; set; }

    /// <summary>
    /// System quantity at the time of counting.
    /// NOTE: Phase 2 — change to decimal when fractional quantities are supported.
    /// </summary>
    public decimal SystemQuantity { get; set; }

    /// <summary>
    /// Actual quantity counted physically.
    /// NOTE: Phase 2 — change to decimal when fractional quantities are supported.
    /// </summary>
    public decimal ActualQuantity { get; set; }

    /// <summary>
    /// Difference = ActualQuantity - SystemQuantity.
    /// NOTE: Phase 2 — recalculate as decimal when fractional quantities are supported.
    /// </summary>
    public decimal Difference => ActualQuantity - SystemQuantity;

    /// <summary>
    /// Reason for the difference (optional)
    /// </summary>
    [MaxLength(500)]
    public string? Reason { get; set; }

    /// <summary>
    /// Optional batch reference when counting at batch level
    /// </summary>
    public int? BatchId { get; set; }

    // Navigation
    public StockTaking StockTaking { get; set; } = null!;
    public Product Product { get; set; } = null!;
    public ProductBatch? Batch { get; set; }
}
