namespace KasserPro.Application.Services.Implementations;

using Microsoft.EntityFrameworkCore;
using KasserPro.Application.Common;
using KasserPro.Application.Common.Interfaces;
using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.RestaurantTables;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Domain.Entities;
using KasserPro.Domain.Enums;

public class RestaurantTableService : IRestaurantTableService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private sealed record OpenTableOrder(int Id, string OrderNumber);

    public RestaurantTableService(IUnitOfWork unitOfWork, ICurrentUserService currentUser)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<List<RestaurantTableDto>>> GetAllAsync(int branchId, CancellationToken ct = default)
    {
        var targetBranchId = branchId > 0 ? branchId : _currentUser.BranchId;

        var openOrders = await _unitOfWork.Orders.Query()
            .AsNoTracking()
            .Where(o => o.TenantId == _currentUser.TenantId
                     && o.BranchId == targetBranchId
                     && o.TableId != null
                     && o.OrderType == OrderType.DineIn
                     && (o.Status == OrderStatus.Draft || o.Status == OrderStatus.Pending))
            .Select(o => new { o.TableId, Order = new OpenTableOrder(o.Id, o.OrderNumber) })
            .ToListAsync(ct);

        var openOrderByTable = openOrders
            .Where(o => o.TableId.HasValue)
            .GroupBy(o => o.TableId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(o => o.Order.Id).First().Order);

        var tables = await _unitOfWork.RestaurantTables.Query()
            .AsNoTracking()
            .Where(t => t.TenantId == _currentUser.TenantId && t.BranchId == targetBranchId)
            .OrderBy(t => t.SortOrder)
            .ThenBy(t => t.Number)
            .ToListAsync(ct);

        return ApiResponse<List<RestaurantTableDto>>.Ok(tables.Select(t => MapToDto(t, openOrderByTable)).ToList());
    }

    public async Task<ApiResponse<RestaurantTableDto>> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var table = await _unitOfWork.RestaurantTables.Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id
                                   && t.TenantId == _currentUser.TenantId
                                   && t.BranchId == _currentUser.BranchId, ct);

        if (table == null)
            return ApiResponse<RestaurantTableDto>.Fail(ErrorCodes.TABLE_NOT_FOUND, ErrorMessages.Get(ErrorCodes.TABLE_NOT_FOUND));

        var openOrder = await GetOpenOrderForTableAsync(table.Id, table.BranchId, ct);
        return ApiResponse<RestaurantTableDto>.Ok(MapToDto(table, openOrder));
    }

    public async Task<ApiResponse<RestaurantTableDto>> CreateAsync(CreateRestaurantTableRequest request, CancellationToken ct = default)
    {
        var targetBranchId = request.BranchId > 0 ? request.BranchId : _currentUser.BranchId;
        var number = request.Number.Trim();

        if (string.IsNullOrWhiteSpace(number))
            return ApiResponse<RestaurantTableDto>.Fail(ErrorCodes.VALIDATION_ERROR, "رقم الطاولة مطلوب");

        var duplicate = await _unitOfWork.RestaurantTables.Query()
            .AnyAsync(t => t.TenantId == _currentUser.TenantId
                        && t.BranchId == targetBranchId
                        && t.Number == number, ct);

        if (duplicate)
            return ApiResponse<RestaurantTableDto>.Fail(ErrorCodes.CONFLICT, "يوجد طاولة بنفس الرقم بالفعل");

        var table = new RestaurantTable
        {
            TenantId = _currentUser.TenantId,
            BranchId = targetBranchId,
            Number = number,
            SortOrder = request.SortOrder,
            Status = RestaurantTableStatus.Available,
            IsActive = true
        };

        await _unitOfWork.RestaurantTables.AddAsync(table);
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<RestaurantTableDto>.Ok(MapToDto(table), "تم إضافة الطاولة بنجاح");
    }

    public async Task<ApiResponse<RestaurantTableDto>> UpdateAsync(int id, UpdateRestaurantTableRequest request, CancellationToken ct = default)
    {
        var table = await _unitOfWork.RestaurantTables.Query()
            .FirstOrDefaultAsync(t => t.Id == id
                                   && t.TenantId == _currentUser.TenantId
                                   && t.BranchId == _currentUser.BranchId, ct);

        if (table == null)
            return ApiResponse<RestaurantTableDto>.Fail(ErrorCodes.TABLE_NOT_FOUND, ErrorMessages.Get(ErrorCodes.TABLE_NOT_FOUND));

        var number = request.Number.Trim();
        if (string.IsNullOrWhiteSpace(number))
            return ApiResponse<RestaurantTableDto>.Fail(ErrorCodes.VALIDATION_ERROR, "رقم الطاولة مطلوب");

        var duplicate = await _unitOfWork.RestaurantTables.Query()
            .AnyAsync(t => t.TenantId == _currentUser.TenantId
                        && t.BranchId == table.BranchId
                        && t.Number == number
                        && t.Id != id, ct);

        if (duplicate)
            return ApiResponse<RestaurantTableDto>.Fail(ErrorCodes.CONFLICT, "يوجد طاولة بنفس الرقم بالفعل");

        table.Number = number;
        table.SortOrder = request.SortOrder;
        table.IsActive = request.IsActive;

        await _unitOfWork.SaveChangesAsync();

        var openOrder = await GetOpenOrderForTableAsync(table.Id, table.BranchId, ct);
        return ApiResponse<RestaurantTableDto>.Ok(MapToDto(table, openOrder), "تم تحديث الطاولة");
    }

    public async Task<ApiResponse<bool>> DeleteAsync(int id, CancellationToken ct = default)
    {
        var table = await _unitOfWork.RestaurantTables.Query()
            .FirstOrDefaultAsync(t => t.Id == id
                                   && t.TenantId == _currentUser.TenantId
                                   && t.BranchId == _currentUser.BranchId, ct);

        if (table == null)
            return ApiResponse<bool>.Fail(ErrorCodes.TABLE_NOT_FOUND, ErrorMessages.Get(ErrorCodes.TABLE_NOT_FOUND));

        var hasOpenOrder = await HasOpenOrderAsync(table.Id, table.BranchId, ct);
        if (hasOpenOrder)
            return ApiResponse<bool>.Fail(ErrorCodes.TABLE_NOT_AVAILABLE, "لا يمكن حذف طاولة عليها طلب مفتوح");

        table.IsDeleted = true;
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<bool>.Ok(true, "تم حذف الطاولة");
    }

    public async Task<ApiResponse<RestaurantTableDto>> SetStatusAsync(int tableId, RestaurantTableStatus status, CancellationToken ct = default)
    {
        var table = await _unitOfWork.RestaurantTables.Query()
            .FirstOrDefaultAsync(t => t.Id == tableId
                                   && t.TenantId == _currentUser.TenantId
                                   && t.BranchId == _currentUser.BranchId, ct);

        if (table == null)
            return ApiResponse<RestaurantTableDto>.Fail(ErrorCodes.TABLE_NOT_FOUND, ErrorMessages.Get(ErrorCodes.TABLE_NOT_FOUND));

        if (status == RestaurantTableStatus.Occupied)
            return ApiResponse<RestaurantTableDto>.Fail(ErrorCodes.TABLE_NOT_AVAILABLE, "لا يمكن إشغال الطاولة يدويًا؛ يتم ذلك عند إنشاء طلب صالة");

        if (await HasOpenOrderAsync(table.Id, table.BranchId, ct))
            return ApiResponse<RestaurantTableDto>.Fail(ErrorCodes.TABLE_NOT_AVAILABLE, "لا يمكن إتاحة الطاولة قبل إنهاء أو إلغاء الطلب المفتوح");

        table.Status = RestaurantTableStatus.Available;
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<RestaurantTableDto>.Ok(MapToDto(table), "تم تحديث حالة الطاولة");
    }

    private async Task<OpenTableOrder?> GetOpenOrderForTableAsync(int tableId, int branchId, CancellationToken ct)
    {
        return await _unitOfWork.Orders.Query()
            .AsNoTracking()
            .Where(o => o.TenantId == _currentUser.TenantId
                     && o.BranchId == branchId
                     && o.TableId == tableId
                     && o.OrderType == OrderType.DineIn
                     && (o.Status == OrderStatus.Draft || o.Status == OrderStatus.Pending))
            .OrderByDescending(o => o.Id)
            .Select(o => new OpenTableOrder(o.Id, o.OrderNumber))
            .FirstOrDefaultAsync(ct);
    }

    private async Task<bool> HasOpenOrderAsync(int tableId, int branchId, CancellationToken ct)
        => await _unitOfWork.Orders.Query()
            .AnyAsync(o => o.TenantId == _currentUser.TenantId
                        && o.BranchId == branchId
                        && o.TableId == tableId
                        && o.OrderType == OrderType.DineIn
                        && (o.Status == OrderStatus.Draft || o.Status == OrderStatus.Pending), ct);

    private static RestaurantTableDto MapToDto(RestaurantTable table, OpenTableOrder? openOrder = null) => new()
    {
        Id = table.Id,
        TenantId = table.TenantId,
        BranchId = table.BranchId,
        Number = table.Number,
        SortOrder = table.SortOrder,
        Status = table.Status,
        IsActive = table.IsActive,
        OpenOrderId = openOrder?.Id,
        OpenOrderNumber = openOrder?.OrderNumber,
        CreatedAt = table.CreatedAt,
        UpdatedAt = table.UpdatedAt
    };

    private static RestaurantTableDto MapToDto(RestaurantTable table, Dictionary<int, OpenTableOrder> openOrderByTable)
        => MapToDto(table, openOrderByTable.TryGetValue(table.Id, out var openOrder) ? openOrder : null);
}
