namespace KasserPro.Domain.Entities;

using KasserPro.Domain.Common;

public class Branch : BaseEntity
{
    public int TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public decimal DefaultTaxRate { get; set; } = 14; // Egypt VAT 14%
    public bool DefaultTaxInclusive { get; set; } = false;
    public string CurrencyCode { get; set; } = "EGP";
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// Is this branch a central warehouse?
    /// Warehouse branches can supply other branches
    /// </summary>
    public bool IsWarehouse { get; set; } = false;

    // Navigation
    public Tenant Tenant { get; set; } = null!;
    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<Shift> Shifts { get; set; } = new List<Shift>();
    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<BranchInventory> Inventories { get; set; } = new List<BranchInventory>();
    public ICollection<BranchProductPrice> ProductPrices { get; set; } = new List<BranchProductPrice>();
    public ICollection<InventoryTransfer> TransfersFrom { get; set; } = new List<InventoryTransfer>();
    public ICollection<InventoryTransfer> TransfersTo { get; set; } = new List<InventoryTransfer>();
}
