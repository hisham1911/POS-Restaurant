namespace KasserPro.Application.DTOs.Reports;

/// <summary>
/// Cashier Performance Report
/// </summary>
public class CashierPerformanceReportDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int BranchId { get; set; }
    public string? BranchName { get; set; }

    public int TotalCashiers { get; set; }
    public int TotalShifts { get; set; }
    public decimal TotalRevenue { get; set; }
    public int TotalOrders { get; set; }

    public List<CashierPerformanceDetailDto> CashierPerformance { get; set; } = new();
}

public class CashierPerformanceDetailDto
{
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    // Shift Statistics
    public int TotalShifts { get; set; }
    public int CompletedShifts { get; set; }
    public int ForceClosedShifts { get; set; }
    public decimal AverageShiftDuration { get; set; } // in hours

    // Sales Performance
    public int TotalOrders { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal AverageOrderValue { get; set; }
    public decimal OrdersPerHour { get; set; }

    // Order Status
    public int CompletedOrders { get; set; }
    public int CancelledOrders { get; set; }
    public int RefundedOrders { get; set; }
    public decimal CancellationRate { get; set; }

    // Payment Methods
    public decimal CashSales { get; set; }
    public decimal CardSales { get; set; }
    public decimal FawrySales { get; set; }

    // Performance Score
    public decimal PerformanceScore { get; set; } // 0-100
    public string PerformanceRating { get; set; } = string.Empty; // "Excellent", "Good", "Average", "Poor"
}

/// <summary>
/// Detailed Shifts Report
/// </summary>
public class DetailedShiftsReportDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int BranchId { get; set; }
    public string? BranchName { get; set; }

    public int TotalShifts { get; set; }
    public int CompletedShifts { get; set; }
    public int ForceClosedShifts { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal AverageShiftRevenue { get; set; }

    public List<DetailedShiftDto> Shifts { get; set; } = new();
}

public class DetailedShiftDto
{
    public int ShiftId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public DateTime OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public decimal Duration { get; set; } // in hours

    // Cash Register
    public decimal OpeningBalance { get; set; }
    public decimal ClosingBalance { get; set; }
    public decimal ExpectedBalance { get; set; }
    public decimal Variance { get; set; }

    // Sales
    public int TotalOrders { get; set; }
    public decimal TotalCash { get; set; }
    public decimal TotalCard { get; set; }
    public decimal TotalFawry { get; set; }
    public decimal TotalBankTransfer { get; set; }
    public decimal TotalSales { get; set; }

    // Status
    public bool IsForceClosed { get; set; }
    public string? ForceCloseReason { get; set; }
    public string? ClosedByUserName { get; set; }
}

/// <summary>
/// Sales by Employee Report
/// </summary>
public class SalesByEmployeeReportDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int BranchId { get; set; }
    public string? BranchName { get; set; }

    public decimal TotalRevenue { get; set; }
    public int TotalOrders { get; set; }
    public int TotalEmployees { get; set; }

    public List<EmployeeSalesDetailDto> EmployeeSales { get; set; } = new();
}

public class EmployeeSalesDetailDto
{
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;

    public int TotalOrders { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal AverageOrderValue { get; set; }
    public decimal RevenuePercentage { get; set; }

    // Daily Breakdown
    public List<DailyEmployeeSalesDto> DailySales { get; set; } = new();
}

public class DailyEmployeeSalesDto
{
    public DateTime Date { get; set; }
    public int Orders { get; set; }
    public decimal Revenue { get; set; }
}
