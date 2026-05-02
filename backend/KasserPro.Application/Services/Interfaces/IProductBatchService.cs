namespace KasserPro.Application.Services.Interfaces;

using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.ProductBatches;

public interface IProductBatchService
{
    Task<ApiResponse<ProductBatchDto>> GetByIdAsync(int id);
    Task<ApiResponse<PagedResult<ProductBatchDto>>> GetAllAsync(int? productId = null, int? branchId = null, string? status = null, int pageNumber = 1, int pageSize = 20);
    Task<ApiResponse<ProductBatchDto>> CreateAsync(CreateProductBatchDto dto);
    Task<ApiResponse<ProductBatchDto>> UpdateAsync(int id, UpdateProductBatchDto dto);
    Task<ApiResponse<bool>> DeleteAsync(int id);
    Task<ApiResponse<ProductBatchDto>> HoldAsync(int id, string reason);
    Task<ApiResponse<ProductBatchDto>> ReleaseAsync(int id, string reason);
    Task<ApiResponse<BatchExpirySummaryDto>> GetExpiryAlertsAsync(int? branchId = null);
    Task<ApiResponse<int>> UpdateExpiredBatchesStatusAsync(CancellationToken ct = default);
    Task<ApiResponse<List<ProductBatchDto>>> GetByProductAsync(int productId, int? branchId = null);
    Task<ApiResponse<List<ProductBatchDto>>> GetAvailableBatchesAsync(int productId, int branchId);
}
