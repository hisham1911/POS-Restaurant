namespace KasserPro.Application.DTOs.Orders;

using KasserPro.Domain.Enums;

public class UpdateOrderStatusRequest
{
    public OrderStatus Status { get; set; }
    public string? Reason { get; set; }
}
