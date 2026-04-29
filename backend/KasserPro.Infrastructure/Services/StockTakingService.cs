namespace KasserPro.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using KasserPro.Application.Common;
using KasserPro.Application.Common.Interfaces;
using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.Inventory;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Domain.Entities;
using KasserPro.Domain.Enums;
using KasserPro.Infrastructure.Data;

public class StockTakingService : IStockTakingService
{
    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public StockTakingService(AppDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<StockTakingDto>> GetAllAsync(int page = 1, int pageSize = 20, string? status = null)
    {
        var query = _context.StockTakings
            .Where(st => st.TenantId == _currentUser.TenantId && st.BranchId == _currentUser.BranchId);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<StockTakingStatus>(status, out var parsed))
            query = query.Where(st => st.Status == parsed);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(st => st.StartedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(st => new StockTakingDto
            {
                Id = st.Id,
                StockTakingNumber = st.StockTakingNumber,
                Status = st.Status,
                StartedAt = st.StartedAt,
                CompletedAt = st.CompletedAt,
                CreatedByUserId = st.CreatedByUserId,
                CreatedByUserName = st.CreatedByUser.Name,
                CompletedByUserId = st.CompletedByUserId,
                CompletedByUserName = st.CompletedByUser != null ? st.CompletedByUser.Name : null,
                Notes = st.Notes,
                ItemCount = st.Items.Count,
                TotalDifference = st.Items.Sum(i => i.ActualQuantity - i.SystemQuantity)
            })
            .ToListAsync();

        return new PagedResult<StockTakingDto>(items, total, page, pageSize);
    }

    public async Task<StockTakingDto?> GetByIdAsync(int id)
    {
        var st = await _context.StockTakings
            .Include(s => s.Items)
            .ThenInclude(i => i.Product)
            .Include(s => s.CreatedByUser)
            .Include(s => s.CompletedByUser)
            .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == _currentUser.TenantId);

        if (st == null) return null;

        return new StockTakingDto
        {
            Id = st.Id,
            StockTakingNumber = st.StockTakingNumber,
            Status = st.Status,
            StartedAt = st.StartedAt,
            CompletedAt = st.CompletedAt,
            CreatedByUserId = st.CreatedByUserId,
            CreatedByUserName = st.CreatedByUser?.Name,
            CompletedByUserId = st.CompletedByUserId,
            CompletedByUserName = st.CompletedByUser?.Name,
            Notes = st.Notes,
            ItemCount = st.Items.Count,
            TotalDifference = st.Items.Sum(i => i.ActualQuantity - i.SystemQuantity),
            Items = st.Items.Select(i => new StockTakingItemDto
            {
                Id = i.Id,
                ProductId = i.ProductId,
                ProductName = i.Product?.Name ?? "",
                ProductSku = i.Product?.Sku,
                SystemQuantity = i.SystemQuantity,
                ActualQuantity = i.ActualQuantity,
                Difference = i.ActualQuantity - i.SystemQuantity,
                Reason = i.Reason,
                BatchId = i.BatchId
            }).ToList()
        };
    }

    public async Task<ApiResponse<StockTakingDto>> CreateAsync(CreateStockTakingRequest request)
    {
        var tenantId = _currentUser.TenantId;
        var branchId = _currentUser.BranchId;
        var userId = _currentUser.UserId;

        if (branchId == 0)
            return ApiResponse<StockTakingDto>.Fail(ErrorCodes.VALIDATION_ERROR, "يجب اختيار الفرع");

        var count = await _context.StockTakings.CountAsync(st => st.TenantId == tenantId);
        var number = $"ST-{DateTime.UtcNow:yyyy}-{(count + 1):D4}";

        var stockTaking = new StockTaking
        {
            TenantId = tenantId,
            BranchId = branchId,
            StockTakingNumber = number,
            Status = StockTakingStatus.InProgress,
            StartedAt = DateTime.UtcNow,
            CreatedByUserId = userId,
            Notes = request.Notes
        };

        _context.StockTakings.Add(stockTaking);
        await _context.SaveChangesAsync();

        var dto = new StockTakingDto
        {
            Id = stockTaking.Id,
            StockTakingNumber = stockTaking.StockTakingNumber,
            Status = stockTaking.Status,
            StartedAt = stockTaking.StartedAt,
            CreatedByUserId = stockTaking.CreatedByUserId,
            Notes = stockTaking.Notes,
            ItemCount = 0,
            TotalDifference = 0
        };

        return ApiResponse<StockTakingDto>.Ok(dto, "تم إنشاء الجرد بنجاح");
    }

    public async Task<ApiResponse<StockTakingItemDto>> UpsertItemAsync(int stockTakingId, UpsertStockTakingItemRequest request)
    {
        var stockTaking = await _context.StockTakings
            .Include(st => st.Items)
            .FirstOrDefaultAsync(st => st.Id == stockTakingId && st.TenantId == _currentUser.TenantId);

        if (stockTaking == null)
            return ApiResponse<StockTakingItemDto>.Fail(ErrorCodes.NOT_FOUND, "جلسة الجرد غير موجودة");

        if (stockTaking.Status != StockTakingStatus.InProgress)
            return ApiResponse<StockTakingItemDto>.Fail(ErrorCodes.VALIDATION_ERROR, "لا يمكن تعديل بنود جرد مكتمل أو ملغى");

        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == request.ProductId && p.TenantId == _currentUser.TenantId);

        if (product == null)
            return ApiResponse<StockTakingItemDto>.Fail(ErrorCodes.PRODUCT_NOT_FOUND, "المنتج غير موجود");

        var inventory = await _context.BranchInventories
            .FirstOrDefaultAsync(bi => bi.BranchId == stockTaking.BranchId && bi.ProductId == request.ProductId);

        var systemQty = inventory?.Quantity ?? 0;

        var existingItem = stockTaking.Items.FirstOrDefault(i => i.ProductId == request.ProductId && i.BatchId == request.BatchId);

        if (existingItem != null)
        {
            existingItem.ActualQuantity = request.ActualQuantity;
            existingItem.Reason = request.Reason;
        }
        else
        {
            existingItem = new StockTakingItem
            {
                StockTakingId = stockTakingId,
                ProductId = request.ProductId,
                SystemQuantity = systemQty,
                ActualQuantity = request.ActualQuantity,
                Reason = request.Reason,
                BatchId = request.BatchId
            };
            _context.StockTakingItems.Add(existingItem);
        }

        await _context.SaveChangesAsync();

        var dto = new StockTakingItemDto
        {
            Id = existingItem.Id,
            ProductId = existingItem.ProductId,
            ProductName = product.Name,
            ProductSku = product.Sku,
            SystemQuantity = existingItem.SystemQuantity,
            ActualQuantity = existingItem.ActualQuantity,
            Difference = existingItem.ActualQuantity - existingItem.SystemQuantity,
            Reason = existingItem.Reason,
            BatchId = existingItem.BatchId
        };

        return ApiResponse<StockTakingItemDto>.Ok(dto, "تم تحديث البند بنجاح");
    }

    public async Task<ApiResponse<bool>> RemoveItemAsync(int stockTakingId, int itemId)
    {
        var stockTaking = await _context.StockTakings
            .Include(st => st.Items)
            .FirstOrDefaultAsync(st => st.Id == stockTakingId && st.TenantId == _currentUser.TenantId);

        if (stockTaking == null)
            return ApiResponse<bool>.Fail(ErrorCodes.NOT_FOUND, "جلسة الجرد غير موجودة");

        if (stockTaking.Status != StockTakingStatus.InProgress)
            return ApiResponse<bool>.Fail(ErrorCodes.VALIDATION_ERROR, "لا يمكن حذف بنود جرد مكتمل أو ملغى");

        var item = stockTaking.Items.FirstOrDefault(i => i.Id == itemId);
        if (item == null)
            return ApiResponse<bool>.Fail(ErrorCodes.NOT_FOUND, "البند غير موجود");

        _context.StockTakingItems.Remove(item);
        await _context.SaveChangesAsync();

        return ApiResponse<bool>.Ok(true, "تم حذف البند بنجاح");
    }

    public async Task<ApiResponse<StockTakingDto>> CompleteAsync(int id, CompleteStockTakingRequest request)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var stockTaking = await _context.StockTakings
                .Include(st => st.Items)
                .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(st => st.Id == id && st.TenantId == _currentUser.TenantId);

            if (stockTaking == null)
                return ApiResponse<StockTakingDto>.Fail(ErrorCodes.NOT_FOUND, "جلسة الجرد غير موجودة");

            if (stockTaking.Status != StockTakingStatus.InProgress)
                return ApiResponse<StockTakingDto>.Fail(ErrorCodes.VALIDATION_ERROR, "جلسة الجرد ليست قيد التنفيذ");

            if (request.ApplyAdjustments)
            {
                foreach (var item in stockTaking.Items)
                {
                    var diff = item.ActualQuantity - item.SystemQuantity;
                    if (diff == 0) continue;

                    var inventory = await _context.BranchInventories
                        .FirstOrDefaultAsync(bi => bi.BranchId == stockTaking.BranchId && bi.ProductId == item.ProductId);

                    if (inventory == null && diff > 0)
                    {
                        inventory = new BranchInventory
                        {
                            TenantId = stockTaking.TenantId,
                            BranchId = stockTaking.BranchId,
                            ProductId = item.ProductId,
                            Quantity = 0,
                            ReorderLevel = item.Product.ReorderPoint ?? 10,
                            LastUpdatedAt = DateTime.UtcNow
                        };
                        _context.BranchInventories.Add(inventory);
                    }

                    if (inventory == null)
                        continue;

                    var balanceBefore = inventory.Quantity;
                    inventory.Quantity = item.ActualQuantity;
                    inventory.LastUpdatedAt = DateTime.UtcNow;

                    var movement = new StockMovement
                    {
                        TenantId = stockTaking.TenantId,
                        BranchId = stockTaking.BranchId,
                        ProductId = item.ProductId,
                        Type = StockMovementType.StockTaking,
                        Quantity = diff,
                        ReferenceType = "StockTaking",
                        ReferenceId = stockTaking.Id,
                        BalanceBefore = balanceBefore,
                        BalanceAfter = inventory.Quantity,
                        Reason = $"جرد رقم {stockTaking.StockTakingNumber}: {item.Reason}",
                        UserId = _currentUser.UserId,
                        BatchId = item.BatchId
                    };
                    _context.StockMovements.Add(movement);
                }
            }

            stockTaking.Status = StockTakingStatus.Completed;
            stockTaking.CompletedAt = DateTime.UtcNow;
            stockTaking.CompletedByUserId = _currentUser.UserId;
            stockTaking.Notes = request.Notes ?? stockTaking.Notes;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            var dto = await GetByIdAsync(id);
            return ApiResponse<StockTakingDto>.Ok(dto!, "تم إتمام الجرد بنجاح");
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            return ApiResponse<StockTakingDto>.Fail(ErrorCodes.INTERNAL_ERROR, "حدث خطأ أثناء إتمام الجرد");
        }
    }

    public async Task<ApiResponse<bool>> CancelAsync(int id)
    {
        var stockTaking = await _context.StockTakings
            .FirstOrDefaultAsync(st => st.Id == id && st.TenantId == _currentUser.TenantId);

        if (stockTaking == null)
            return ApiResponse<bool>.Fail(ErrorCodes.NOT_FOUND, "جلسة الجرد غير موجودة");

        if (stockTaking.Status != StockTakingStatus.InProgress)
            return ApiResponse<bool>.Fail(ErrorCodes.VALIDATION_ERROR, "لا يمكن إلغاء جرد مكتمل أو ملغى بالفعل");

        stockTaking.Status = StockTakingStatus.Cancelled;
        stockTaking.CompletedAt = DateTime.UtcNow;
        stockTaking.CompletedByUserId = _currentUser.UserId;

        await _context.SaveChangesAsync();
        return ApiResponse<bool>.Ok(true, "تم إلغاء الجرد بنجاح");
    }
}
