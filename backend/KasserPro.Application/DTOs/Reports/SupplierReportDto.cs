namespace KasserPro.Application.DTOs.Reports;

/// <summary>
/// Supplier Purchases Report
/// </summary>
public class SupplierPurchasesReportDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int BranchId { get; set; }
    public string? BranchName { get; set; }
    
    public int TotalSuppliers { get; set; }
    public int ActiveSuppliers { get; set; }
    public decimal TotalPurchases { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal TotalOutstanding { get; set; }
    public int TotalInvoices { get; set; }
    
    public List<SupplierPurchaseDetailDto> SupplierDetails { get; set; } = new();
}

public class SupplierPurchaseDetailDto
{
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public int InvoiceCount { get; set; }
    public decimal TotalPurchases { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal Outstanding { get; set; }
    public DateTime? LastPurchaseDate { get; set; }
    public int ProductCount { get; set; }
}

/// <summary>
/// Supplier Debts Report (Money we owe to suppliers)
/// </summary>
public class SupplierDebtsReportDto
{
    public DateTime ReportDate { get; set; }
    public int BranchId { get; set; }
    public string? BranchName { get; set; }
    
    public int TotalSuppliersWithDebt { get; set; }
    public decimal TotalOutstandingAmount { get; set; }
    public decimal TotalOverdueAmount { get; set; }
    public int OverdueInvoicesCount { get; set; }
    
    public List<SupplierDebtDetailDto> SupplierDebts { get; set; } = new();
}

public class SupplierDebtDetailDto
{
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public decimal TotalDue { get; set; }
    public int UnpaidInvoicesCount { get; set; }
    public DateTime? OldestUnpaidInvoiceDate { get; set; }
    public int DaysSinceOldestInvoice { get; set; }
    public DateTime? LastPaymentDate { get; set; }
}

/// <summary>
/// Supplier Performance Report
/// </summary>
public class SupplierPerformanceReportDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int BranchId { get; set; }
    public string? BranchName { get; set; }
    
    public List<SupplierPerformanceDetailDto> SupplierPerformance { get; set; } = new();
}

public class SupplierPerformanceDetailDto
{
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public int TotalInvoices { get; set; }
    public decimal TotalPurchaseValue { get; set; }
    public decimal AverageInvoiceValue { get; set; }
    public int UniqueProductsSupplied { get; set; }
    public decimal OnTimePaymentRate { get; set; }
    public int DaysAveragePaymentDelay { get; set; }
    public string ReliabilityScore { get; set; } = string.Empty; // "Excellent", "Good", "Fair", "Poor"
}
