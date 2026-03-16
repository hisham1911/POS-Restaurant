namespace KasserPro.Application.Services.Interfaces;

using KasserPro.Application.Common;
using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.Inventory;

public interface IInventoryService
{
    // Branch Inventory Queries
    Task<ApiResponse<List<BranchInventoryDto>>> GetBranchInventoryAsync(int branchId);
    Task<ApiResponse<BranchInventorySummaryDto>> GetProductInventoryAcrossBranchesAsync(int productId);
    Task<ApiResponse<List<BranchInventoryDto>>> GetLowStockItemsAsync(int? branchId = null);

    // Inventory Adjustments
    Task<ApiResponse<BranchInventoryDto>> AdjustInventoryAsync(AdjustInventoryRequest request);

    // Inventory Transfers
    Task<ApiResponse<InventoryTransferDto>> CreateTransferAsync(CreateTransferRequest request);
    Task<ApiResponse<InventoryTransferDto>> ApproveTransferAsync(int transferId);
    Task<ApiResponse<InventoryTransferDto>> ReceiveTransferAsync(int transferId);
    Task<ApiResponse<InventoryTransferDto>> CancelTransferAsync(int transferId, CancelTransferRequest request);
    Task<ApiResponse<InventoryTransferDto>> GetTransferByIdAsync(int transferId);
    Task<ApiResponse<PaginatedResponse<InventoryTransferDto>>> GetTransfersAsync(
        int? fromBranchId = null,
        int? toBranchId = null,
        string? status = null,
        int pageNumber = 1,
        int pageSize = 20);

    // Branch Prices
    Task<ApiResponse<List<BranchProductPriceDto>>> GetBranchPricesAsync(int branchId);
    Task<ApiResponse<BranchProductPriceDto>> SetBranchPriceAsync(SetBranchPriceRequest request);
    Task<ApiResponse<bool>> RemoveBranchPriceAsync(int branchId, int productId);

    // Helper Methods
    Task<decimal> GetEffectivePriceAsync(int productId, int branchId);
    Task<int> GetAvailableQuantityAsync(int productId, int branchId);

    // Legacy compatibility methods for OrderService
    Task BatchDecrementStockAsync(List<(int ProductId, int Quantity)> items, int orderId);
    Task<int> GetCurrentStockAsync(int productId);
    Task<int> IncrementStockAsync(int productId, int quantity, int referenceId);

    /// <summary>
    /// FIX H-2: Get restorable quantity for a product on a given order.
    /// Returns actualDecremented - alreadyRestored to prevent stock inflation from clamping.
    /// </summary>
    Task<int> GetRestorableQuantityAsync(int productId, int orderId);
}
