namespace KasserPro.Application.Services.Interfaces;

using KasserPro.Application.DTOs;
using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.Orders;

public interface IOrderService
{
    Task<ApiResponse<OrderDto>> CreateAsync(CreateOrderRequest request, int userId);
    Task<ApiResponse<OrderDto>> GetByIdAsync(int id);
    Task<ApiResponse<List<OrderDto>>> GetTodayOrdersAsync();
    Task<ApiResponse<PagedResult<OrderDto>>> GetAllAsync(string? status = null, string? orderType = null, DateTime? fromDate = null, DateTime? toDate = null, int page = 1, int pageSize = 20);
    Task<ApiResponse<PagedResult<OrderDto>>> GetByCustomerIdAsync(int customerId, int page = 1, int pageSize = 10);
    Task<ApiResponse<OrderDto>> AddItemAsync(int orderId, AddOrderItemRequest request);
    Task<ApiResponse<OrderDto>> AddCustomItemAsync(int orderId, AddCustomItemRequest request);
    Task<ApiResponse<OrderDto>> RemoveItemAsync(int orderId, int itemId);
    Task<ApiResponse<OrderDto>> CompleteAsync(int orderId, CompleteOrderRequest request);
    Task<ApiResponse<bool>> CancelAsync(int orderId, string? reason);
    Task<ApiResponse<OrderDto>> RefundAsync(int orderId, int userId, string? reason, List<RefundItemDto>? items = null);
    Task<ApiResponse<OrderDto>> MarkAsDeliveredAsync(int orderId);
}
