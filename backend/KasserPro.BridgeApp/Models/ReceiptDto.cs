using System.Text.Json.Serialization;

namespace KasserPro.BridgeApp.Models;

/// <summary>
/// Receipt data for printing
/// </summary>
public class ReceiptDto
{
    [JsonPropertyName("receiptNumber")]
    public string ReceiptNumber { get; set; } = string.Empty;
    [JsonPropertyName("branchName")]
    public string BranchName { get; set; } = string.Empty;
    [JsonPropertyName("date")]
    public DateTime Date { get; set; }
    [JsonPropertyName("items")]
    public List<ReceiptItemDto> Items { get; set; } = new();
    [JsonPropertyName("netTotal")]
    public decimal NetTotal { get; set; }
    [JsonPropertyName("taxAmount")]
    public decimal TaxAmount { get; set; }
    [JsonPropertyName("totalAmount")]
    public decimal TotalAmount { get; set; }
    [JsonPropertyName("amountPaid")]
    public decimal AmountPaid { get; set; }
    [JsonPropertyName("changeAmount")]
    public decimal ChangeAmount { get; set; }
    [JsonPropertyName("amountDue")]
    public decimal AmountDue { get; set; }
    [JsonPropertyName("paymentMethod")]
    public string PaymentMethod { get; set; } = string.Empty;
    [JsonPropertyName("cashierName")]
    public string CashierName { get; set; } = string.Empty;
    [JsonPropertyName("customerName")]
    public string CustomerName { get; set; } = string.Empty;
    [JsonPropertyName("isRefund")]
    public bool IsRefund { get; set; } = false;
    [JsonPropertyName("refundReason")]
    public string? RefundReason { get; set; }
    [JsonPropertyName("isKitchenTicket")]
    public bool IsKitchenTicket { get; set; } = false;
    [JsonPropertyName("kitchenTitle")]
    public string? KitchenTitle { get; set; }
    [JsonPropertyName("orderNotes")]
    public string? OrderNotes { get; set; }
    [JsonPropertyName("isAdditionTicket")]
    public bool IsAdditionTicket { get; set; } = false;
}

/// <summary>
/// Individual item on a receipt
/// </summary>
public class ReceiptItemDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    [JsonPropertyName("quantity")]
    public decimal Quantity { get; set; }
    [JsonPropertyName("unitPrice")]
    public decimal UnitPrice { get; set; }
    [JsonPropertyName("totalPrice")]
    public decimal TotalPrice { get; set; }
    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
    [JsonPropertyName("isAddOn")]
    public bool IsAddOn { get; set; }
}
