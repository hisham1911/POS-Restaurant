namespace KasserPro.Application.DTOs.Orders;

public class KitchenTicketDto
{
    public int OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string TicketType { get; set; } = string.Empty;
    public string Header { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public int KitchenPrintCount { get; set; }
    public DateTime PrintedAt { get; set; }
    public List<KitchenTicketItemDto> Items { get; set; } = new();
}

public class KitchenTicketItemDto
{
    public int OrderItemId { get; set; }
    public int? ParentOrderItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string? Notes { get; set; }
    public bool IsCustomItem { get; set; }
    public bool IsAddOn { get; set; }
}
