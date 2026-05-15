namespace KasserPro.Application.DTOs.Products;

public class AdjustStockRequest
{
    public decimal Quantity { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string AdjustmentType { get; set; } = "Adjustment";
}

public class StockAdjustResultDto
{
    public decimal NewBalance { get; set; }
    public decimal PreviousBalance { get; set; }
    public decimal Change { get; set; }
}
