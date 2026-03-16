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

    // Calculated fields
    public int DurationHours { get; set; }
    public int DurationMinutes { get; set; }

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
}

public class OpenShiftRequest
{
    public decimal OpeningBalance { get; set; }
}

public class CloseShiftRequest
{
    public decimal ClosingBalance { get; set; }
    public string? Notes { get; set; }
}
