namespace KasserPro.Application.DTOs.PurchaseInvoices;

using KasserPro.Domain.Enums;

public class AddPaymentRequest
{
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    public PaymentMethod Method { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }
}
