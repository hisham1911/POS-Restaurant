namespace KasserPro.Domain.Entities;

using System.ComponentModel.DataAnnotations;
using KasserPro.Domain.Common;
using KasserPro.Domain.Enums;

/// <summary>
/// Represents a physical inventory count (stock taking) session.
/// </summary>
public class StockTaking : BaseEntity
{
    public int TenantId { get; set; }
    public int BranchId { get; set; }

    /// <summary>
    /// Unique stock taking number (e.g., ST-2026-0001)
    /// </summary>
    [MaxLength(50)]
    public string StockTakingNumber { get; set; } = string.Empty;

    public StockTakingStatus Status { get; set; } = StockTakingStatus.InProgress;

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// User who created/started the stock taking
    /// </summary>
    public int CreatedByUserId { get; set; }

    /// <summary>
    /// User who completed/applied the stock taking
    /// </summary>
    public int? CompletedByUserId { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    // Navigation
    public Tenant Tenant { get; set; } = null!;
    public Branch Branch { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
    public User? CompletedByUser { get; set; }
    public ICollection<StockTakingItem> Items { get; set; } = new List<StockTakingItem>();
}
