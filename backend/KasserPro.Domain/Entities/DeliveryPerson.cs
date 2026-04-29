namespace KasserPro.Domain.Entities;

using KasserPro.Domain.Common;

public class DeliveryPerson : BaseEntity
{
    public int TenantId { get; set; }
    public int BranchId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? VehicleInfo { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation
    public Tenant Tenant { get; set; } = null!;
    public Branch Branch { get; set; } = null!;
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
