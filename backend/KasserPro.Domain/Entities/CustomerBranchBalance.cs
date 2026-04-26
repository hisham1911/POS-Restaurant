namespace KasserPro.Domain.Entities;

using KasserPro.Domain.Common;

public class CustomerBranchBalance : BaseEntity
{
    public int CustomerId { get; set; }
    public int BranchId { get; set; }
    public int TenantId { get; set; }
    public decimal AmountDue { get; set; }

    public Customer? Customer { get; set; }
}
