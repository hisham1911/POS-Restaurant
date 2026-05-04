namespace KasserPro.Domain.Entities;

using KasserPro.Domain.Common;

public class WalletTransaction : BaseEntity
{
    public int WalletId { get; set; }
    public int TenantId { get; set; }
    public int BranchId { get; set; }
    public string Type { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal BalanceBefore { get; set; }
    public decimal BalanceAfter { get; set; }
    public string? ReferenceType { get; set; }
    public int? ReferenceId { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Description { get; set; }
    public int? UserId { get; set; }
    public string? UserName { get; set; }

    public Wallet Wallet { get; set; } = null!;
}
