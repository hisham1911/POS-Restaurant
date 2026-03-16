namespace KasserPro.Application.Services.Interfaces;

using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.Reports;

public interface IFinancialReportService
{
    /// <summary>
    /// Get Profit & Loss report for a date range
    /// </summary>
    Task<ApiResponse<ProfitLossReportDto>> GetProfitLossReportAsync(DateTime fromDate, DateTime toDate);
    
    /// <summary>
    /// Get detailed expenses report
    /// </summary>
    Task<ApiResponse<ExpensesReportDto>> GetExpensesReportAsync(DateTime fromDate, DateTime toDate);
}
