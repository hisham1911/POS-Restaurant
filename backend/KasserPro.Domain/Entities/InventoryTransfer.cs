namespace KasserPro.Domain.Entities;

using KasserPro.Domain.Common;
using KasserPro.Domain.Enums;

/// <summary>
/// Represents a transfer of inventory between branches.
/// Only Admin can create and approve transfers.
/// </summary>
public class InventoryTransfer : BaseEntity
{
    /// <summary>
    /// Tenant ID for multi-tenancy
    /// </summary>
    public int TenantId { get; set; }
    
    /// <summary>
    /// Transfer number (auto-generated: IT-2026-0001)
    /// </summary>
    public string TransferNumber { get; set; } = string.Empty;
    
    /// <summary>
    /// Source branch ID
    /// </summary>
    public int FromBranchId { get; set; }
    
    /// <summary>
    /// Destination branch ID
    /// </summary>
    public int ToBranchId { get; set; }
    
    /// <summary>
    /// Product being transferred
    /// </summary>
    public int ProductId { get; set; }
    
    /// <summary>
    /// Product snapshot at transfer time
    /// </summary>
    public string ProductName { get; set; } = string.Empty;
    public string? ProductSku { get; set; }
    
    /// <summary>
    /// Quantity to transfer
    /// </summary>
    public decimal Quantity { get; set; }
    
    /// <summary>
    /// Current status of the transfer
    /// </summary>
    public InventoryTransferStatus Status { get; set; } = InventoryTransferStatus.Pending;
    
    /// <summary>
    /// Reason for transfer
    /// </summary>
    public string Reason { get; set; } = string.Empty;
    
    public string? Notes { get; set; }
    
    /// <summary>
    /// User who initiated the transfer
    /// </summary>
    public int CreatedByUserId { get; set; }
    public string CreatedByUserName { get; set; } = string.Empty;
    
    /// <summary>
    /// User who approved the transfer (Admin only)
    /// </summary>
    public int? ApprovedByUserId { get; set; }
    public string? ApprovedByUserName { get; set; }
    public DateTime? ApprovedAt { get; set; }
    
    /// <summary>
    /// User who received the transfer at destination
    /// </summary>
    public int? ReceivedByUserId { get; set; }
    public string? ReceivedByUserName { get; set; }
    public DateTime? ReceivedAt { get; set; }
    
    /// <summary>
    /// User who cancelled the transfer
    /// </summary>
    public int? CancelledByUserId { get; set; }
    public string? CancelledByUserName { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    
    // Navigation Properties
    public Tenant Tenant { get; set; } = null!;
    public Branch FromBranch { get; set; } = null!;
    public Branch ToBranch { get; set; } = null!;
    public Product Product { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
    public User? ApprovedByUser { get; set; }
    public User? ReceivedByUser { get; set; }
}
