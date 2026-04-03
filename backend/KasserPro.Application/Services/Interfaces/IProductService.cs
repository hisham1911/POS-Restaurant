namespace KasserPro.Application.Services.Interfaces;

using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.Products;

public interface IProductService
{
    Task<ApiResponse<PagedResult<ProductDto>>> GetAllAsync(
        int? categoryId = null,
        string? search = null,
        bool? isActive = null,
        bool? lowStock = null,
        int page = 1,
        int pageSize = 20);
    Task<ApiResponse<ProductDto>> GetByIdAsync(int id);
    Task<ApiResponse<ProductDto>> CreateAsync(CreateProductRequest request);
    Task<ApiResponse<ProductDto>> UpdateAsync(int id, UpdateProductRequest request);
    Task<ApiResponse<bool>> DeleteAsync(int id);
    Task<ApiResponse<StockAdjustResultDto>> AdjustStockAsync(int id, AdjustStockRequest request);
    Task<ApiResponse<ProductDto>> QuickCreateAsync(QuickCreateProductRequest request);
}
