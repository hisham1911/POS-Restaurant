namespace KasserPro.Application.DTOs.PurchaseInvoices;

public class CreatePurchaseInvoiceRequest
{
    public int SupplierId { get; set; }
    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;
    public List<CreatePurchaseInvoiceItemRequest> Items { get; set; } = new();
    public string? Notes { get; set; }
}

public class CreatePurchaseInvoiceItemRequest
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal SellingPrice { get; set; }
    public string? Notes { get; set; }
}
