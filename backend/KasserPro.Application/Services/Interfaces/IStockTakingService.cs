namespace KasserPro.Application.Services.Interfaces;

using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.Inventory;

public interface IStockTakingService
{
    Task<PagedResult<StockTakingDto>> GetAllAsync(int page = 1, int pageSize = 20, string? status = null);
    Task<StockTakingDto?> GetByIdAsync(int id);
    Task<ApiResponse<StockTakingDto>> CreateAsync(CreateStockTakingRequest request);
    Task<ApiResponse<StockTakingItemDto>> UpsertItemAsync(int stockTakingId, UpsertStockTakingItemRequest request);
    Task<ApiResponse<bool>> RemoveItemAsync(int stockTakingId, int itemId);
    Task<ApiResponse<StockTakingDto>> CompleteAsync(int id, CompleteStockTakingRequest request);
    Task<ApiResponse<bool>> CancelAsync(int id);
}
