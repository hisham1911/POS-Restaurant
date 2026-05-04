namespace KasserPro.Application.DTOs.Shifts;

public record ShiftProductSummaryDto
{
    public string ProductName { get; init; } = string.Empty;
    public decimal TotalQuantity { get; init; }
    public decimal TotalAmount { get; init; }
}
