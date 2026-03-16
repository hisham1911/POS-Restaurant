namespace KasserPro.Application.DTOs.Reports;

/// <summary>
/// Profit & Loss Report
/// </summary>
public class ProfitLossReportDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int BranchId { get; set; }
    public string? BranchName { get; set; }
    
    // Revenue
    public decimal GrossSales { get; set; }
    public decimal TotalDiscount { get; set; }
    public decimal NetSales { get; set; }
    public decimal TotalTax { get; set; }
    public decimal TotalRevenue { get; set; }
    
    // Cost of Goods Sold
    public decimal TotalCost { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal GrossProfitMargin { get; set; } // Percentage
    
    // Operating Expenses
    public decimal TotalExpenses { get; set; }
    public List<ExpenseCategoryBreakdownDto> ExpensesByCategory { get; set; } = new();
    
    // Net Profit
    public decimal NetProfit { get; set; }
    public decimal NetProfitMargin { get; set; } // Percentage
    
    // Additional Metrics
    public int TotalOrders { get; set; }
    public decimal AverageOrderValue { get; set; }
    public decimal RefundsAmount { get; set; }
}

public class ExpenseCategoryBreakdownDto
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public int ExpenseCount { get; set; }
    public decimal Percentage { get; set; }
}

/// <summary>
/// Expenses Report
/// </summary>
public class ExpensesReportDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int BranchId { get; set; }
    public string? BranchName { get; set; }
    
    public decimal TotalExpenses { get; set; }
    public int TotalExpenseCount { get; set; }
    public decimal AverageExpenseAmount { get; set; }
    
    // Breakdown by Category
    public List<ExpenseCategoryBreakdownDto> ExpensesByCategory { get; set; } = new();
    
    // Breakdown by Payment Method
    public decimal CashExpenses { get; set; }
    public decimal CardExpenses { get; set; }
    public decimal OtherExpenses { get; set; }
    
    // Daily Breakdown
    public List<DailyExpenseDto> DailyExpenses { get; set; } = new();
    
    // Top Expenses
    public List<ExpenseDetailDto> TopExpenses { get; set; } = new();
}

public class DailyExpenseDto
{
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public int Count { get; set; }
}

public class ExpenseDetailDto
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string? RecipientName { get; set; }
}
