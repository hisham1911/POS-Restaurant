namespace KasserPro.Application.Services.Interfaces;

using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.Reports;

public interface IProductReportService
{
    /// <summary>
    /// Get product movement report
    /// </summary>
    Task<ApiResponse<ProductMovementReportDto>> GetProductMovementReportAsync(
        DateTime fromDate, 
        DateTime toDate, 
        int? categoryId = null);
    
    /// <summary>
    /// Get most profitable products report
    /// </summary>
    Task<ApiResponse<ProfitableProductsReportDto>> GetProfitableProductsReportAsync(
        DateTime fromDate, 
        DateTime toDate, 
        int topCount = 10);
    
    /// <summary>
    /// Get slow-moving products report
    /// </summary>
    Task<ApiResponse<SlowMovingProductsReportDto>> GetSlowMovingProductsReportAsync(
        int daysThreshold = 30);
    
    /// <summary>
    /// Get Cost of Goods Sold (COGS) report
    /// </summary>
    Task<ApiResponse<CogsReportDto>> GetCogsReportAsync(DateTime fromDate, DateTime toDate);
}
