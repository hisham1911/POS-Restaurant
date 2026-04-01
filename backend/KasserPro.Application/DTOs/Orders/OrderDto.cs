namespace KasserPro.Application.DTOs.Orders;

public class OrderDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string OrderType { get; set; } = string.Empty;

    // Branch Snapshot
    public int BranchId { get; set; }
    public string? BranchName { get; set; }
    public string? BranchAddress { get; set; }
    public string? BranchPhone { get; set; }

    // Currency
    public string CurrencyCode { get; set; } = "EGP";

    // Totals
    public decimal Subtotal { get; set; }
    public string? DiscountType { get; set; }
    public decimal? DiscountValue { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? DiscountCode { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal ServiceChargePercent { get; set; }
    public decimal ServiceChargeAmount { get; set; }
    public decimal Total { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal AmountDue { get; set; }
    public decimal ChangeAmount { get; set; }

    // Customer
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public int? CustomerId { get; set; }
    public string? Notes { get; set; }

    // User
    public int UserId { get; set; }
    public string? UserName { get; set; }

    // Shift
    public int? ShiftId { get; set; }

    // Timestamps
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }

    // Refund Information
    public DateTime? RefundedAt { get; set; }
    public string? RefundReason { get; set; }
    public decimal RefundAmount { get; set; }
    public int? RefundedByUserId { get; set; }
    public string? RefundedByUserName { get; set; }

    /// <summary>
    /// For Return orders: the ID of the original order that was refunded.
    /// </summary>
    public int? OriginalOrderId { get; set; }

    public List<OrderItemDto> Items { get; set; } = new();
    public List<PaymentDto> Payments { get; set; } = new();
}

public class OrderItemDto
{
    public int Id { get; set; }
    public int? ProductId { get; set; }

    // Custom Item Fields
    public bool IsCustomItem { get; set; }
    public string? CustomName { get; set; }
    public decimal? CustomUnitPrice { get; set; }
    public decimal? CustomTaxRate { get; set; }

    // Product Snapshot
    public string ProductName { get; set; } = string.Empty;
    public string? ProductNameEn { get; set; }
    public string? ProductSku { get; set; }
    public string? ProductBarcode { get; set; }

    // Price Snapshot
    public decimal UnitPrice { get; set; }
    public decimal OriginalPrice { get; set; }
    public int Quantity { get; set; }
    public int RefundedQuantity { get; set; }

    // Discount
    public string? DiscountType { get; set; }
    public decimal? DiscountValue { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? DiscountReason { get; set; }

    // Tax
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public bool TaxInclusive { get; set; }

    public decimal Subtotal { get; set; }
    public decimal Total { get; set; }
    public string? Notes { get; set; }
}

public class PaymentDto
{
    public int Id { get; set; }
    public string Method { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Reference { get; set; }
    public DateTime CreatedAt { get; set; }
}
