namespace KasserPro.Application.DTOs.PurchaseInvoices;

public class PurchaseInvoiceDto
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string? SupplierPhone { get; set; }
    public DateTime InvoiceDate { get; set; }
    public string Status { get; set; } = string.Empty;
    
    public decimal Subtotal { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal AmountDue { get; set; }
    
    public string? Notes { get; set; }
    
    public string CreatedByUserName { get; set; } = string.Empty;
    public string? ConfirmedByUserName { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    
    public DateTime CreatedAt { get; set; }
    
    public List<PurchaseInvoiceItemDto> Items { get; set; } = new();
    public List<PurchaseInvoicePaymentDto> Payments { get; set; } = new();
}

public class PurchaseInvoiceItemDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ProductSku { get; set; }
    public int Quantity { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal SellingPrice { get; set; }
    public decimal Total { get; set; }
    public string? Notes { get; set; }
}

public class PurchaseInvoicePaymentDto
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; }
    public string Method { get; set; } = string.Empty;
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }
    public string CreatedByUserName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
