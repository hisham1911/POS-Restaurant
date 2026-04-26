namespace KasserPro.Domain.Entities;

using KasserPro.Domain.Common;
using KasserPro.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;

/// <summary>
/// Tracks all cash register transactions for complete audit trail
/// </summary>
public class CashRegisterTransaction : BaseEntity
{
    public int TenantId { get; set; }
    public int BranchId { get; set; }
    
    /// <summary>
    /// Auto-generated transaction number (e.g., CRT-2026-0001)
    /// </summary>
    public string TransactionNumber { get; set; } = string.Empty;
    
    /// <summary>
    /// Type of transaction
    /// </summary>
    public CashRegisterTransactionType Type { get; set; }
    
    /// <summary>
    /// Transaction amount (positive for deposits, negative for withdrawals)
    /// </summary>
    public decimal Amount { get; set; }
    
    /// <summary>
    /// Cash register balance before this transaction
    /// </summary>
    public decimal BalanceBefore { get; set; }
    
    /// <summary>
    /// Cash register balance after this transaction
    /// </summary>
    public decimal BalanceAfter { get; set; }
    
    /// <summary>
    /// Date and time of the transaction
    /// </summary>
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Description/reason for the transaction
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Type of reference entity ("Order", "Expense", "PurchaseInvoice", etc.)
    /// </summary>
    public string? ReferenceType { get; set; }
    
    /// <summary>
    /// ID of the reference entity
    /// </summary>
    public int? ReferenceId { get; set; }
    
    /// <summary>
    /// Optional link to shift
    /// </summary>
    public int? ShiftId { get; set; }
    
    /// <summary>
    /// For transfer transactions, links the two transactions together
    /// </summary>
    public int? TransferReferenceId { get; set; }

    [NotMapped]
    public bool IsTransferOut =>
        Type == CashRegisterTransactionType.Transfer &&
        string.Equals(ReferenceType, "TransferOut", StringComparison.OrdinalIgnoreCase);
    
    // Audit trail
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    
    // Navigation properties
    public Tenant Tenant { get; set; } = null!;
    public Branch Branch { get; set; } = null!;
    public Shift? Shift { get; set; }
    public User User { get; set; } = null!;
}
