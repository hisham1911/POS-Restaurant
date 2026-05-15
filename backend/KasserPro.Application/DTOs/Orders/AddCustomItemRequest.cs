namespace KasserPro.Application.DTOs.Orders;

/// <summary>
/// Request to add a custom POS item (not from product catalog)
/// </summary>
public class AddCustomItemRequest
{
    public string Name { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; } = 1;
    public int? ParentOrderItemId { get; set; }
    public decimal? TaxRate { get; set; } // null = use tenant default
    public bool? TaxInclusive { get; set; } // Ignored - custom items are always tax exclusive
    public string? Notes { get; set; }
}
