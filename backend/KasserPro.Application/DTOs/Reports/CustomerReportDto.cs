namespace KasserPro.Application.DTOs.Reports;

/// <summary>
/// Top Customers Report
/// </summary>
public class TopCustomersReportDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int BranchId { get; set; }
    public string? BranchName { get; set; }
    
    public int TotalCustomers { get; set; }
    public int ActiveCustomers { get; set; }
    public int NewCustomers { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal AverageCustomerValue { get; set; }
    
    public List<TopCustomerDto> TopCustomers { get; set; } = new();
}

public class TopCustomerDto
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public int TotalOrders { get; set; }
    public decimal TotalSpent { get; set; }
    public decimal AverageOrderValue { get; set; }
    public DateTime? LastOrderDate { get; set; }
    public decimal OutstandingBalance { get; set; }
}

/// <summary>
/// Customer Debts Report
/// </summary>
public class CustomerDebtsReportDto
{
    public DateTime ReportDate { get; set; }
    public int BranchId { get; set; }
    public string? BranchName { get; set; }
    
    public int TotalCustomersWithDebt { get; set; }
    public decimal TotalOutstandingAmount { get; set; }
    public decimal TotalOverdueAmount { get; set; }
    public int OverdueCustomersCount { get; set; }
    
    public List<CustomerDebtDetailDto> CustomerDebts { get; set; } = new();
    public List<AgingBracketDto> AgingAnalysis { get; set; } = new();
}

public class CustomerDebtDetailDto
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public decimal TotalDue { get; set; }
    public decimal CreditLimit { get; set; }
    public int DaysSinceLastOrder { get; set; }
    public DateTime? LastOrderDate { get; set; }
    public DateTime? OldestUnpaidOrderDate { get; set; }
    public int UnpaidOrdersCount { get; set; }
    public bool IsOverLimit { get; set; }
}

public class AgingBracketDto
{
    public string Bracket { get; set; } = string.Empty; // "0-30 days", "31-60 days", etc.
    public int CustomerCount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal Percentage { get; set; }
}

/// <summary>
/// Customer Activity Report
/// </summary>
public class CustomerActivityReportDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int BranchId { get; set; }
    public string? BranchName { get; set; }
    
    // Customer Segmentation
    public int NewCustomers { get; set; }
    public int ReturningCustomers { get; set; }
    public int InactiveCustomers { get; set; }
    
    // Activity Metrics
    public decimal NewCustomerRevenue { get; set; }
    public decimal ReturningCustomerRevenue { get; set; }
    public decimal AverageNewCustomerValue { get; set; }
    public decimal AverageReturningCustomerValue { get; set; }
    
    // Retention
    public decimal RetentionRate { get; set; }
    public decimal ChurnRate { get; set; }
    
    public List<CustomerSegmentDto> CustomerSegments { get; set; } = new();
}

public class CustomerSegmentDto
{
    public string SegmentName { get; set; } = string.Empty;
    public int CustomerCount { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal AverageOrderValue { get; set; }
    public int TotalOrders { get; set; }
}
