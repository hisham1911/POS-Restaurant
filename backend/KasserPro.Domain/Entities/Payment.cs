namespace KasserPro.Domain.Entities;

using KasserPro.Domain.Common;
using KasserPro.Domain.Enums;

public class Payment : BaseEntity
{
    public int TenantId { get; set; }
    public int BranchId { get; set; }
    
    public PaymentMethod Method { get; set; }
    public decimal Amount { get; set; }
    public string? Reference { get; set; }

    public int OrderId { get; set; }
    public int? WalletId { get; set; }

    // Navigation
    public Tenant Tenant { get; set; } = null!;
    public Branch Branch { get; set; } = null!;
    public Order Order { get; set; } = null!;
    public Wallet? Wallet { get; set; }
}
