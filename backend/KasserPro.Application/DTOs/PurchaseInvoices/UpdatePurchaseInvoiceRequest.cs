namespace KasserPro.Application.DTOs.PurchaseInvoices;

public class UpdatePurchaseInvoiceRequest
{
    public int SupplierId { get; set; }
    public DateTime InvoiceDate { get; set; }
    public List<UpdatePurchaseInvoiceItemRequest> Items { get; set; } = new();
    public string? Notes { get; set; }
}

public class UpdatePurchaseInvoiceItemRequest
{
    public int? Id { get; set; } // null = new item
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal SellingPrice { get; set; }
    public string? Notes { get; set; }
}
