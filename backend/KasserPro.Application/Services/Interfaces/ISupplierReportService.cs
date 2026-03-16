namespace KasserPro.Application.Services.Interfaces;

using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.Reports;

public interface ISupplierReportService
{
    /// <summary>
    /// Get supplier purchases report
    /// </summary>
    Task<ApiResponse<SupplierPurchasesReportDto>> GetSupplierPurchasesReportAsync(
        DateTime fromDate, 
        DateTime toDate);
    
    /// <summary>
    /// Get supplier debts report
    /// </summary>
    Task<ApiResponse<SupplierDebtsReportDto>> GetSupplierDebtsReportAsync();
    
    /// <summary>
    /// Get supplier performance report
    /// </summary>
    Task<ApiResponse<SupplierPerformanceReportDto>> GetSupplierPerformanceReportAsync(
        DateTime fromDate, 
        DateTime toDate);
}
