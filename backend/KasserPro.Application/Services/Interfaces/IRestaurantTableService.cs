namespace KasserPro.Application.Services.Interfaces;

using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.RestaurantTables;
using KasserPro.Domain.Enums;

public interface IRestaurantTableService
{
    Task<ApiResponse<List<RestaurantTableDto>>> GetAllAsync(int branchId, CancellationToken ct = default);
    Task<ApiResponse<RestaurantTableDto>> GetByIdAsync(int id, CancellationToken ct = default);
    Task<ApiResponse<RestaurantTableDto>> CreateAsync(CreateRestaurantTableRequest request, CancellationToken ct = default);
    Task<ApiResponse<RestaurantTableDto>> UpdateAsync(int id, UpdateRestaurantTableRequest request, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteAsync(int id, CancellationToken ct = default);
    Task<ApiResponse<RestaurantTableDto>> SetStatusAsync(int tableId, RestaurantTableStatus status, CancellationToken ct = default);
}
