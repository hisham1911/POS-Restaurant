namespace KasserPro.Domain.Entities;

using KasserPro.Domain.Common;

public class Wallet : BaseEntity
{
    public int TenantId { get; set; }
    public int BranchId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? AccountNumber { get; set; }
    public string Type { get; set; } = string.Empty;
    public decimal CurrentBalance { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public Branch Branch { get; set; } = null!;
    public ICollection<WalletTransaction> Transactions { get; set; } = new List<WalletTransaction>();
}
