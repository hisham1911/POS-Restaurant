namespace KasserPro.Domain.Entities;

using KasserPro.Domain.Common;

public class SavedOrderNote : BaseEntity
{
    public int TenantId { get; set; }
    public int BranchId { get; set; }
    public string Text { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public Tenant Tenant { get; set; } = null!;
    public Branch Branch { get; set; } = null!;
}
