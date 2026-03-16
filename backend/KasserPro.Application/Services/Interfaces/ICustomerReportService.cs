namespace KasserPro.Application.Services.Interfaces;

using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.Reports;

public interface ICustomerReportService
{
    /// <summary>
    /// Get top customers report
    /// </summary>
    Task<ApiResponse<TopCustomersReportDto>> GetTopCustomersReportAsync(
        DateTime fromDate, 
        DateTime toDate, 
        int topCount = 20);
    
    /// <summary>
    /// Get customer debts report
    /// </summary>
    Task<ApiResponse<CustomerDebtsReportDto>> GetCustomerDebtsReportAsync();
    
    /// <summary>
    /// Get customer activity report
    /// </summary>
    Task<ApiResponse<CustomerActivityReportDto>> GetCustomerActivityReportAsync(
        DateTime fromDate, 
        DateTime toDate);
}
