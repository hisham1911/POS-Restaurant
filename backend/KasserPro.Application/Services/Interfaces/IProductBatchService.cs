namespace KasserPro.Application.Services.Interfaces;

using KasserPro.Application.DTOs;
using KasserPro.Application.DTOs.Common;

public interface IProductBatchService
{
    Task<ApiResponse<ProductBatchDto>> GetByIdAsync(int id);
    Task<ApiResponse<PagedResult<ProductBatchDto>>> GetAllAsync(int? productId = null, int? branchId = null, string? status = null, int pageNumber = 1, int pageSize = 20);
    Task<ApiResponse<ProductBatchDto>> CreateAsync(CreateProductBatchDto dto);
    Task<ApiResponse<bool>> DeleteAsync(int id);
    Task<ApiResponse<BatchExpirySummaryDto>> GetExpiryAlertsAsync(int? branchId = null);
    Task<ApiResponse<List<ProductBatchDto>>> GetByProductAsync(int productId, int? branchId = null);
}
