using Microsoft.EntityFrameworkCore;
using KasserPro.Application.Common;
using KasserPro.Application.Common.Interfaces;
using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.Delivery;
using KasserPro.Application.DTOs.Orders;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Domain.Entities;
using KasserPro.Domain.Enums;

namespace KasserPro.Application.Services.Implementations;

public class DeliveryService : IDeliveryService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public DeliveryService(IUnitOfWork unitOfWork, ICurrentUserService currentUser)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<DeliveryPersonDto>> GetAllAsync(
        int page = 1,
        int pageSize = 20,
        string? search = null,
        CancellationToken ct = default)
    {
        var query = _unitOfWork.DeliveryPersons.Query()
            .AsNoTracking()
            .Where(dp => dp.TenantId == _currentUser.TenantId
                      && dp.BranchId == _currentUser.BranchId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLowerInvariant();
            query = query.Where(dp => dp.Name.ToLower().Contains(normalizedSearch)
                                   || dp.Phone.Contains(search));
        }

        var safePage = page < 1 ? 1 : page;
        var safePageSize = pageSize < 1 ? 20 : pageSize;

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(dp => dp.CreatedAt)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Select(dp => new DeliveryPersonDto
            {
                Id = dp.Id,
                Name = dp.Name,
                Phone = dp.Phone,
                VehicleInfo = dp.VehicleInfo,
                IsActive = dp.IsActive,
                CreatedAt = dp.CreatedAt
            })
            .ToListAsync(ct);

        return new PagedResult<DeliveryPersonDto>(items, total, safePage, safePageSize);
    }

    public async Task<DeliveryPersonDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await _unitOfWork.DeliveryPersons.Query()
            .AsNoTracking()
            .Where(dp => dp.Id == id
                      && dp.TenantId == _currentUser.TenantId
                      && dp.BranchId == _currentUser.BranchId)
            .Select(dp => new DeliveryPersonDto
            {
                Id = dp.Id,
                Name = dp.Name,
                Phone = dp.Phone,
                VehicleInfo = dp.VehicleInfo,
                IsActive = dp.IsActive,
                CreatedAt = dp.CreatedAt
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<ApiResponse<DeliveryPersonDto>> CreateAsync(
        CreateDeliveryPersonRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return ApiResponse<DeliveryPersonDto>.Fail(
                ErrorCodes.VALIDATION_ERROR,
                "اسم المندوب مطلوب");
        }

        if (string.IsNullOrWhiteSpace(request.Phone))
        {
            return ApiResponse<DeliveryPersonDto>.Fail(
                ErrorCodes.VALIDATION_ERROR,
                "رقم هاتف المندوب مطلوب");
        }

        var phone = request.Phone.Trim();
        var deliveryPersonExists = await _unitOfWork.DeliveryPersons.Query()
            .AnyAsync(dp => dp.TenantId == _currentUser.TenantId
                         && dp.BranchId == _currentUser.BranchId
                         && dp.Phone == phone, ct);

        if (deliveryPersonExists)
        {
            return ApiResponse<DeliveryPersonDto>.Fail(
                ErrorCodes.VALIDATION_ERROR,
                "رقم الهاتف مستخدم بالفعل");
        }

        var deliveryPerson = new DeliveryPerson
        {
            TenantId = _currentUser.TenantId,
            BranchId = _currentUser.BranchId,
            Name = request.Name.Trim(),
            Phone = phone,
            VehicleInfo = request.VehicleInfo?.Trim()
        };

        await _unitOfWork.DeliveryPersons.AddAsync(deliveryPerson);
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<DeliveryPersonDto>.Ok(MapToDto(deliveryPerson), "تم الإضافة بنجاح");
    }

    public async Task<ApiResponse<DeliveryPersonDto>> UpdateAsync(
        int id,
        UpdateDeliveryPersonRequest request,
        CancellationToken ct = default)
    {
        var deliveryPerson = await _unitOfWork.DeliveryPersons.Query()
            .FirstOrDefaultAsync(dp => dp.Id == id
                                    && dp.TenantId == _currentUser.TenantId
                                    && dp.BranchId == _currentUser.BranchId, ct);

        if (deliveryPerson == null)
        {
            return ApiResponse<DeliveryPersonDto>.Fail(ErrorCodes.NOT_FOUND, "المندوب غير موجود");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return ApiResponse<DeliveryPersonDto>.Fail(
                ErrorCodes.VALIDATION_ERROR,
                "اسم المندوب مطلوب");
        }

        if (string.IsNullOrWhiteSpace(request.Phone))
        {
            return ApiResponse<DeliveryPersonDto>.Fail(
                ErrorCodes.VALIDATION_ERROR,
                "رقم هاتف المندوب مطلوب");
        }

        var phone = request.Phone.Trim();
        var duplicatePhoneExists = await _unitOfWork.DeliveryPersons.Query()
            .AnyAsync(dp => dp.TenantId == _currentUser.TenantId
                         && dp.BranchId == _currentUser.BranchId
                         && dp.Phone == phone
                         && dp.Id != id, ct);

        if (duplicatePhoneExists)
        {
            return ApiResponse<DeliveryPersonDto>.Fail(
                ErrorCodes.VALIDATION_ERROR,
                "رقم الهاتف مستخدم بالفعل");
        }

        deliveryPerson.Name = request.Name.Trim();
        deliveryPerson.Phone = phone;
        deliveryPerson.VehicleInfo = request.VehicleInfo?.Trim();
        deliveryPerson.IsActive = request.IsActive;
        deliveryPerson.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<DeliveryPersonDto>.Ok(MapToDto(deliveryPerson), "تم التحديث");
    }

    public async Task<ApiResponse<bool>> DeleteAsync(int id, CancellationToken ct = default)
    {
        var deliveryPerson = await _unitOfWork.DeliveryPersons.Query()
            .FirstOrDefaultAsync(dp => dp.Id == id
                                    && dp.TenantId == _currentUser.TenantId
                                    && dp.BranchId == _currentUser.BranchId, ct);

        if (deliveryPerson == null)
        {
            return ApiResponse<bool>.Fail(ErrorCodes.NOT_FOUND, "المندوب غير موجود");
        }

        deliveryPerson.IsDeleted = true;
        deliveryPerson.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<bool>.Ok(true, "تم الحذف");
    }

    public async Task<List<DeliveryPersonDto>> GetActiveDeliveryPersonsAsync(CancellationToken ct = default)
    {
        return await _unitOfWork.DeliveryPersons.Query()
            .AsNoTracking()
            .Where(dp => dp.TenantId == _currentUser.TenantId
                      && dp.BranchId == _currentUser.BranchId
                      && dp.IsActive)
            .OrderBy(dp => dp.Name)
            .Select(dp => new DeliveryPersonDto
            {
                Id = dp.Id,
                Name = dp.Name,
                Phone = dp.Phone,
                VehicleInfo = dp.VehicleInfo,
                IsActive = dp.IsActive,
                CreatedAt = dp.CreatedAt
            })
            .ToListAsync(ct);
    }

    public async Task<ApiResponse<OrderDto>> AssignDeliveryPersonAsync(
        int orderId,
        AssignDeliveryRequest request,
        CancellationToken ct = default)
    {
        var order = await _unitOfWork.Orders.Query()
            .Include(o => o.DeliveryPerson)
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.Id == orderId
                                   && o.TenantId == _currentUser.TenantId
                                   && o.BranchId == _currentUser.BranchId, ct);

        if (order == null)
        {
            return ApiResponse<OrderDto>.Fail(
                ErrorCodes.ORDER_NOT_FOUND,
                ErrorMessages.Get(ErrorCodes.ORDER_NOT_FOUND));
        }

        if (order.OrderType != OrderType.Delivery)
        {
            return ApiResponse<OrderDto>.Fail(
                ErrorCodes.DELIVERY_ORDER_TYPE_INVALID,
                ErrorMessages.Get(ErrorCodes.DELIVERY_ORDER_TYPE_INVALID));
        }

        var deliveryPerson = await _unitOfWork.DeliveryPersons.Query()
            .FirstOrDefaultAsync(dp => dp.Id == request.DeliveryPersonId
                                    && dp.TenantId == _currentUser.TenantId
                                    && dp.BranchId == _currentUser.BranchId, ct);

        if (deliveryPerson == null)
        {
            return ApiResponse<OrderDto>.Fail(ErrorCodes.NOT_FOUND, "المندوب غير موجود");
        }

        if (!deliveryPerson.IsActive)
        {
            return ApiResponse<OrderDto>.Fail(
                ErrorCodes.DELIVERY_PERSON_INACTIVE,
                ErrorMessages.Get(ErrorCodes.DELIVERY_PERSON_INACTIVE));
        }

        order.DeliveryPersonId = request.DeliveryPersonId;
        order.DeliveryPerson = deliveryPerson;
        order.DeliveryStatus = DeliveryStatus.Assigned;
        order.AssignedAt = DateTime.UtcNow;
        order.DeliveryNotes = request.DeliveryNotes ?? order.DeliveryNotes;

        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<OrderDto>.Ok(MapToOrderDto(order), "تم التعيين");
    }

    public async Task<ApiResponse<OrderDto>> UpdateDeliveryStatusAsync(
        int orderId,
        UpdateDeliveryStatusRequest request,
        CancellationToken ct = default)
    {
        var order = await _unitOfWork.Orders.Query()
            .Include(o => o.DeliveryPerson)
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.Id == orderId
                                   && o.TenantId == _currentUser.TenantId
                                   && o.BranchId == _currentUser.BranchId, ct);

        if (order == null)
        {
            return ApiResponse<OrderDto>.Fail(
                ErrorCodes.ORDER_NOT_FOUND,
                ErrorMessages.Get(ErrorCodes.ORDER_NOT_FOUND));
        }

        if (!Enum.TryParse<DeliveryStatus>(request.DeliveryStatus, true, out var newStatus))
        {
            return ApiResponse<OrderDto>.Fail(
                ErrorCodes.VALIDATION_ERROR,
                "حالة التوصيل غير صالحة");
        }

        if (order.OrderType != OrderType.Delivery)
        {
            return ApiResponse<OrderDto>.Fail(
                ErrorCodes.DELIVERY_ORDER_TYPE_INVALID,
                ErrorMessages.Get(ErrorCodes.DELIVERY_ORDER_TYPE_INVALID));
        }

        if (newStatus == DeliveryStatus.OutForDelivery && order.DeliveryPersonId == null)
        {
            return ApiResponse<OrderDto>.Fail(
                ErrorCodes.DELIVERY_NO_PERSON_ASSIGNED,
                ErrorMessages.Get(ErrorCodes.DELIVERY_NO_PERSON_ASSIGNED));
        }

        if (newStatus == DeliveryStatus.Delivered
            && order.DeliveryStatus != DeliveryStatus.Assigned
            && order.DeliveryStatus != DeliveryStatus.OutForDelivery)
        {
            return ApiResponse<OrderDto>.Fail(
                ErrorCodes.DELIVERY_INVALID_STATUS_TRANSITION,
                ErrorMessages.Get(ErrorCodes.DELIVERY_INVALID_STATUS_TRANSITION));
        }

        if (order.DeliveryStatus == DeliveryStatus.Delivered
            || order.DeliveryStatus == DeliveryStatus.Cancelled)
        {
            return ApiResponse<OrderDto>.Fail(
                ErrorCodes.DELIVERY_STATUS_FINAL,
                ErrorMessages.Get(ErrorCodes.DELIVERY_STATUS_FINAL));
        }

        order.DeliveryStatus = newStatus;
        order.DeliveryNotes = request.DeliveryNotes ?? order.DeliveryNotes;

        if (newStatus == DeliveryStatus.Delivered)
        {
            order.DeliveredAt = DateTime.UtcNow;
        }
        else if (newStatus == DeliveryStatus.OutForDelivery && order.AssignedAt == null)
        {
            order.AssignedAt = DateTime.UtcNow;
        }

        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<OrderDto>.Ok(MapToOrderDto(order), "تم التحديث");
    }

    public async Task<ApiResponse<PagedResult<OrderDto>>> GetDeliveryOrdersAsync(
        DeliveryOrderFilters filters,
        CancellationToken ct)
    {
        var safePage = filters.Page < 1 ? 1 : filters.Page;
        var safePageSize = filters.PageSize < 1 ? 20 : filters.PageSize;

        var query = _unitOfWork.Orders.Query()
            .AsNoTracking()
            .Include(o => o.DeliveryPerson)
            .Where(o => o.TenantId == _currentUser.TenantId
                     && o.BranchId == _currentUser.BranchId
                     && o.OrderType == OrderType.Delivery);

        if (!string.IsNullOrWhiteSpace(filters.Status)
            && Enum.TryParse<DeliveryStatus>(filters.Status, true, out var parsedStatus))
        {
            query = query.Where(o => o.DeliveryStatus == parsedStatus);
        }

        if (filters.DeliveryPersonId.HasValue)
        {
            query = query.Where(o => o.DeliveryPersonId == filters.DeliveryPersonId.Value);
        }

        if (filters.Date.HasValue)
        {
            var filterDate = filters.Date.Value.Date;
            query = query.Where(o => o.CreatedAt.Date == filterDate);
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Select(o => new OrderDto
            {
                Id = o.Id,
                OrderNumber = o.OrderNumber,
                Status = o.Status.ToString(),
                OrderType = o.OrderType.ToString(),
                CustomerName = o.CustomerName,
                CustomerPhone = o.CustomerPhone,
                Total = o.Total,
                DeliveryAddress = o.DeliveryAddress,
                DeliveryFee = o.DeliveryFee,
                DeliveryStatus = o.DeliveryStatus != null ? o.DeliveryStatus.ToString() : null,
                DeliveryNotes = o.DeliveryNotes,
                DeliveryPersonId = o.DeliveryPersonId,
                DeliveryPersonName = o.DeliveryPerson != null ? o.DeliveryPerson.Name : null,
                AssignedAt = o.AssignedAt,
                DeliveredAt = o.DeliveredAt,
                CreatedAt = o.CreatedAt
            })
            .ToListAsync(ct);

        var pagedResult = new PagedResult<OrderDto>(items, totalCount, safePage, safePageSize);
        return ApiResponse<PagedResult<OrderDto>>.Ok(pagedResult);
    }

    private static DeliveryPersonDto MapToDto(DeliveryPerson deliveryPerson) => new()
    {
        Id = deliveryPerson.Id,
        Name = deliveryPerson.Name,
        Phone = deliveryPerson.Phone,
        VehicleInfo = deliveryPerson.VehicleInfo,
        IsActive = deliveryPerson.IsActive,
        CreatedAt = deliveryPerson.CreatedAt
    };

    private static OrderDto MapToOrderDto(Order order) => new()
    {
        Id = order.Id,
        OrderNumber = order.OrderNumber,
        Status = order.Status.ToString(),
        OrderType = order.OrderType.ToString(),
        BranchId = order.BranchId,
        BranchName = order.BranchName,
        BranchAddress = order.BranchAddress,
        BranchPhone = order.BranchPhone,
        CurrencyCode = order.CurrencyCode,
        Subtotal = order.Subtotal,
        DiscountType = order.DiscountType,
        DiscountValue = order.DiscountValue,
        DiscountAmount = order.DiscountAmount,
        DiscountCode = order.DiscountCode,
        TaxRate = order.TaxRate,
        TaxAmount = order.TaxAmount,
        ServiceChargePercent = order.ServiceChargePercent,
        ServiceChargeAmount = order.ServiceChargeAmount,
        Total = order.Total,
        AmountPaid = order.AmountPaid,
        AmountDue = order.AmountDue,
        ChangeAmount = order.ChangeAmount,
        CustomerName = order.CustomerName,
        CustomerPhone = order.CustomerPhone,
        CustomerId = order.CustomerId,
        Notes = order.Notes,
        UserId = order.UserId,
        UserName = order.UserName,
        ShiftId = order.ShiftId,
        CreatedAt = order.CreatedAt,
        CompletedAt = order.CompletedAt,
        CancelledAt = order.CancelledAt,
        CancellationReason = order.CancellationReason,
        RefundedAt = order.RefundedAt,
        RefundReason = order.RefundReason,
        RefundAmount = order.RefundAmount,
        RefundedByUserId = order.RefundedByUserId,
        RefundedByUserName = order.RefundedByUserName,
        OriginalOrderId = order.OriginalOrderId,
        DeliveryPersonId = order.DeliveryPersonId,
        DeliveryPersonName = order.DeliveryPerson?.Name,
        DeliveryAddress = order.DeliveryAddress,
        DeliveryFee = order.DeliveryFee,
        DeliveryStatus = order.DeliveryStatus?.ToString(),
        DeliveryNotes = order.DeliveryNotes,
        AssignedAt = order.AssignedAt,
        DeliveredAt = order.DeliveredAt,
        Items = order.Items.Select(i => new OrderItemDto
        {
            Id = i.Id,
            ProductId = i.ProductId,
            IsCustomItem = i.IsCustomItem,
            CustomName = i.CustomName,
            CustomUnitPrice = i.CustomUnitPrice,
            CustomTaxRate = i.CustomTaxRate,
            ProductName = i.ProductName,
            ProductNameEn = i.ProductNameEn,
            ProductSku = i.ProductSku,
            ProductBarcode = i.ProductBarcode,
            UnitPrice = i.UnitPrice,
            OriginalPrice = i.OriginalPrice,
            Quantity = i.Quantity,
            RefundedQuantity = i.RefundedQuantity,
            DiscountType = i.DiscountType,
            DiscountValue = i.DiscountValue,
            DiscountAmount = i.DiscountAmount,
            DiscountReason = i.DiscountReason,
            TaxRate = i.TaxRate,
            TaxAmount = i.TaxAmount,
            TaxInclusive = i.TaxInclusive,
            Subtotal = i.Subtotal,
            Total = i.Total,
            Notes = i.Notes
        }).ToList(),
        Payments = order.Payments.Select(p => new PaymentDto
        {
            Id = p.Id,
            Method = p.Method.ToString(),
            Amount = p.Amount,
            Reference = p.Reference,
            CreatedAt = p.CreatedAt
        }).ToList()
    };
}
