namespace KasserPro.Application.Services.Interfaces;

using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.Reports;

public interface IEmployeeReportService
{
    /// <summary>
    /// Get cashier performance report
    /// </summary>
    Task<ApiResponse<CashierPerformanceReportDto>> GetCashierPerformanceReportAsync(
        DateTime fromDate, 
        DateTime toDate);
    
    /// <summary>
    /// Get detailed shifts report
    /// </summary>
    Task<ApiResponse<DetailedShiftsReportDto>> GetDetailedShiftsReportAsync(
        DateTime fromDate, 
        DateTime toDate, 
        int? userId = null);
    
    /// <summary>
    /// Get sales by employee report
    /// </summary>
    Task<ApiResponse<SalesByEmployeeReportDto>> GetSalesByEmployeeReportAsync(
        DateTime fromDate, 
        DateTime toDate);
}
