namespace KasserPro.Application.Services.Interfaces;

using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.Delivery;
using KasserPro.Application.DTOs.Orders;

public interface IDeliveryService
{
    Task<PagedResult<DeliveryPersonDto>> GetAllAsync(int page = 1, int pageSize = 20, string? search = null, CancellationToken ct = default);
    Task<DeliveryPersonDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<ApiResponse<DeliveryPersonDto>> CreateAsync(CreateDeliveryPersonRequest request, CancellationToken ct = default);
    Task<ApiResponse<DeliveryPersonDto>> UpdateAsync(int id, UpdateDeliveryPersonRequest request, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteAsync(int id, CancellationToken ct = default);
    Task<ApiResponse<OrderDto>> AssignDeliveryPersonAsync(int orderId, AssignDeliveryRequest request, CancellationToken ct = default);
    Task<ApiResponse<OrderDto>> UpdateDeliveryStatusAsync(int orderId, UpdateDeliveryStatusRequest request, CancellationToken ct = default);
    Task<List<DeliveryPersonDto>> GetActiveDeliveryPersonsAsync(CancellationToken ct = default);
    Task<ApiResponse<PagedResult<OrderDto>>> GetDeliveryOrdersAsync(DeliveryOrderFilters filters, CancellationToken ct);
}
