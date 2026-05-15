namespace KasserPro.Domain.Entities;

using KasserPro.Domain.Common;
using KasserPro.Domain.Enums;

public class RestaurantTable : BaseEntity
{
    public int TenantId { get; set; }
    public int BranchId { get; set; }
    public string Number { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public RestaurantTableStatus Status { get; set; } = RestaurantTableStatus.Available;
    public bool IsActive { get; set; } = true;

    public Tenant Tenant { get; set; } = null!;
    public Branch Branch { get; set; } = null!;
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
