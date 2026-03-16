namespace KasserPro.Domain.Entities;

using System.ComponentModel.DataAnnotations;
using KasserPro.Domain.Common;
using KasserPro.Domain.Enums;

public class Order : BaseEntity
{
    /// <summary>
    /// Concurrency token for optimistic locking.
    /// Prevents race conditions: double-complete, double-refund, concurrent state changes.
    /// </summary>
    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public int TenantId { get; set; }
    public int BranchId { get; set; }

    // Branch Snapshot (for receipts/reports)
    public string? BranchName { get; set; }
    public string? BranchAddress { get; set; }
    public string? BranchPhone { get; set; }

    public string OrderNumber { get; set; } = string.Empty;
    public OrderStatus Status { get; set; } = OrderStatus.Draft;
    public OrderType OrderType { get; set; } = OrderType.DineIn;

    // Currency Snapshot
    public string CurrencyCode { get; set; } = "EGP";

    public decimal Subtotal { get; set; }

    // Discount Snapshot
    public string? DiscountType { get; set; }
    public decimal? DiscountValue { get; set; }
    public decimal DiscountAmount { get; set; } = 0;
    public string? DiscountCode { get; set; }
    public int? DiscountId { get; set; }

    // Tax Snapshot
    public decimal TaxRate { get; set; } = 14;
    public decimal TaxAmount { get; set; }

    // Service Charge
    public decimal ServiceChargePercent { get; set; } = 0;
    public decimal ServiceChargeAmount { get; set; } = 0;

    public decimal Total { get; set; }

    public decimal AmountPaid { get; set; } = 0;
    public decimal AmountDue { get; set; } = 0;
    public decimal ChangeAmount { get; set; } = 0;

    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public int? CustomerId { get; set; }
    public string? Notes { get; set; }

    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }

    // Refund Information
    public DateTime? RefundedAt { get; set; }
    public string? RefundReason { get; set; }
    public int? RefundedByUserId { get; set; }
    public string? RefundedByUserName { get; set; }
    public decimal RefundAmount { get; set; } = 0;

    /// <summary>
    /// For Return orders: links back to the original order that was refunded.
    /// Null for regular (non-return) orders.
    /// </summary>
    public int? OriginalOrderId { get; set; }

    public int UserId { get; set; }
    public string? UserName { get; set; } // Snapshot
    public int? ShiftId { get; set; }
    public int? CompletedByUserId { get; set; }

    // Navigation
    public Tenant Tenant { get; set; } = null!;
    public Branch Branch { get; set; } = null!;
    public User User { get; set; } = null!;
    public Shift? Shift { get; set; }
    public Customer? Customer { get; set; }
    public Order? OriginalOrder { get; set; }
    public RefundLog? RefundLog { get; set; }
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
