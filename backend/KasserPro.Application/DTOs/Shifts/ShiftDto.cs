namespace KasserPro.Application.DTOs.Shifts;

using KasserPro.Application.DTOs.Orders;

public class ShiftDto
{
    public int Id { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal ClosingBalance { get; set; }
    public decimal ExpectedBalance { get; set; }
    public decimal Difference { get; set; }
    public DateTime OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public bool IsClosed { get; set; }
    public string? Notes { get; set; }
    public decimal TotalCash { get; set; }
    public decimal TotalCard { get; set; }
    public decimal TotalFawry { get; set; }
    public decimal TotalBankTransfer { get; set; }
    public decimal TotalSales { get; set; }
    public decimal TotalCollected { get; set; }
    public decimal DeferredAmount { get; set; }
    public decimal CollectedCash { get; set; }
    public decimal CollectedCard { get; set; }
    public decimal CollectedFawry { get; set; }
    public decimal CollectedBankTransfer { get; set; }
    public int TotalOrders { get; set; }
    public string UserName { get; set; } = string.Empty;

    // Inactivity tracking
    public DateTime LastActivityAt { get; set; }
    public int InactiveHours { get; set; } // Calculated

    // Force close
    public bool IsForceClosed { get; set; }
    public string? ForceClosedByUserName { get; set; }
    public DateTime? ForceClosedAt { get; set; }
    public string? ForceCloseReason { get; set; }

    // Handover
    public bool IsHandedOver { get; set; }
    public string? HandedOverFromUserName { get; set; }
    public string? HandedOverToUserName { get; set; }
    public DateTime? HandedOverAt { get; set; }
    public decimal HandoverBalance { get; set; }
    public string? HandoverNotes { get; set; }

    // Reconciliation
    public bool IsReconciled { get; set; }
    public string? ReconciledByUserName { get; set; }
    public DateTime? ReconciledAt { get; set; }

    // Calculated fields
    public int DurationHours { get; set; }
    public int DurationMinutes { get; set; }

    // Detailed sales by payment method (Task 5.2)
    public decimal TotalCashSales { get; set; }
    public decimal TotalCardSales { get; set; }
    public decimal TotalFawrySales { get; set; }
    public decimal TotalBankTransferSales { get; set; }
    public decimal TotalVodafoneCashSales { get; set; }

    // Refunds (Task 2.3 + 5.2)
    public decimal TotalRefunds { get; set; }
    public int RefundsCount { get; set; }

    // Orders breakdown (Task 5.2)
    public int TotalOrdersCount { get; set; }
    public int CompletedOrdersCount { get; set; }
    public int CancelledOrdersCount { get; set; }
    public int RefundedOrdersCount { get; set; }

    // Credit sales (Task 5.2)
    public decimal TotalCreditSales { get; set; }
    public int CreditOrdersCount { get; set; }

    // Net cash (Task 5.2)
    public decimal NetCash { get; set; }

    // Expenses (Task 2.3)
    public decimal TotalExpenses { get; set; }

    // Debt Payments (سداد الديون)
    public decimal TotalDebtPayments { get; set; }
    public int DebtPaymentsCount { get; set; }
    public decimal TotalDebtPaymentsCash { get; set; }
    public decimal TotalDebtPaymentsCard { get; set; }
    public decimal TotalDebtPaymentsFawry { get; set; }
    public decimal TotalDebtPaymentsBankTransfer { get; set; }

    // Concurrency Token (serialized as Base64 string in JSON)
    public byte[] RowVersion { get; set; } = [];

    // Orders in this shift
    public List<ShiftOrderDto> Orders { get; set; } = new();
}

/// <summary>
/// Simplified order DTO for shift context (less data than full OrderDto)
/// </summary>
public class ShiftOrderDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string OrderType { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public string? CustomerName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
}

public class OpenShiftRequest
{
    public decimal OpeningBalance { get; set; }
}

public class CloseShiftRequest
{
    public decimal ClosingBalance { get; set; }
    public string? Notes { get; set; }
    public byte[]? RowVersion { get; set; }
}
