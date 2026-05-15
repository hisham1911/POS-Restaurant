namespace KasserPro.Application.DTOs.Orders;

/// <summary>
/// Print command sent to Desktop Bridge App via SignalR
/// </summary>
public class PrintCommandDto
{
    public string CommandId { get; set; } = string.Empty;
    public ReceiptDto Receipt { get; set; } = new();
    public ReceiptPrintSettings Settings { get; set; } = new();
}

/// <summary>
/// Receipt print settings from tenant configuration
/// </summary>
public class ReceiptPrintSettings
{
    public string PaperSize { get; set; } = "80mm";
    public int? CustomWidth { get; set; }
    public int HeaderFontSize { get; set; } = 12;
    public int BodyFontSize { get; set; } = 9;
    public int TotalFontSize { get; set; } = 11;
    public bool ShowBranchName { get; set; } = true;
    public bool ShowCashier { get; set; } = true;
    public bool ShowThankYou { get; set; } = true;
    public bool ShowCustomerName { get; set; } = true;
    public bool ShowLogo { get; set; } = true;
    public string? FooterMessage { get; set; }
    public string? PhoneNumber { get; set; }
    public string? LogoUrl { get; set; }
    public decimal TaxRate { get; set; } = 14;
    public bool IsTaxEnabled { get; set; } = true;
}

/// <summary>
/// Receipt data for printing
/// </summary>
public class ReceiptDto
{
    public string ReceiptNumber { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public List<ReceiptItemDto> Items { get; set; } = new();
    public decimal ItemDiscountsTotal { get; set; }
    public string? DiscountType { get; set; }
    public decimal? DiscountValue { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal NetTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal ChangeAmount { get; set; }
    public decimal AmountDue { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string CashierName { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public bool IsRefund { get; set; } = false;
    public string? RefundReason { get; set; }
    public string? OrderType { get; set; }
    public string? DeliveryAddress { get; set; }
    public decimal DeliveryFee { get; set; }
    public string? DeliveryNotes { get; set; }
    public string? DeliveryStatus { get; set; }
    public bool IsKitchenTicket { get; set; } = false;
    public string? KitchenTitle { get; set; }
    public string? OrderNotes { get; set; }
    public bool IsAdditionTicket { get; set; } = false;
}

/// <summary>
/// Individual item on a receipt
/// </summary>
public class ReceiptItemDto
{
    public string Name { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public string? DiscountType { get; set; }
    public decimal? DiscountValue { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? DiscountReason { get; set; }
    public string? Notes { get; set; }
    public bool IsAddOn { get; set; }
}
