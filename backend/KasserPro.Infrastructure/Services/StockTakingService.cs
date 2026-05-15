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
        var stockTakings = await query
            .AsNoTracking()
            .Include(st => st.Category)
            .Include(st => st.CreatedByUser)
            .Include(st => st.CompletedByUser)
            .Include(st => st.Items)
            .OrderByDescending(st => st.StartedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();
        var items = stockTakings.Select(st => MapToDto(st)).ToList();

        return new PagedResult<StockTakingDto>(items, total, page, pageSize);
    }

    public async Task<StockTakingDto?> GetByIdAsync(int id)
    {
        var st = await _context.StockTakings
            .AsNoTracking()
            .Include(s => s.Category)
            .Include(s => s.Items)
            .ThenInclude(i => i.Product)
            .Include(s => s.Items)
            .ThenInclude(i => i.Batch)
            .Include(s => s.CreatedByUser)
            .Include(s => s.CompletedByUser)
            .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == _currentUser.TenantId && s.BranchId == _currentUser.BranchId);

        if (st == null) return null;

        return MapToDto(st, includeItems: true);
    }

    public async Task<ApiResponse<StockTakingDto>> CreateAsync(CreateStockTakingRequest request)
    {
        var tenantId = _currentUser.TenantId;
        var branchId = _currentUser.BranchId;
        var userId = _currentUser.UserId;

        if (branchId == 0)
            return ApiResponse<StockTakingDto>.Fail(ErrorCodes.VALIDATION_ERROR, "يجب اختيار الفرع");

        if (request.Type == StockTakingType.Partial && !request.CategoryId.HasValue)
            return ApiResponse<StockTakingDto>.Fail(ErrorCodes.VALIDATION_ERROR, "يجب اختيار الفئة عند الجرد الجزئي");

        if (request.CategoryId.HasValue)
        {
            var categoryExists = await _context.Categories
                .AnyAsync(c => c.Id == request.CategoryId.Value && c.TenantId == tenantId);
            if (!categoryExists)
                return ApiResponse<StockTakingDto>.Fail(ErrorCodes.CATEGORY_NOT_FOUND, ErrorMessages.Get(ErrorCodes.CATEGORY_NOT_FOUND));
        }

        var count = await _context.StockTakings.CountAsync(st => st.TenantId == tenantId);
        var number = $"ST-{DateTime.UtcNow:yyyy}-{(count + 1):D4}";

        var stockTaking = new StockTaking
        {
            TenantId = tenantId,
            BranchId = branchId,
            StockTakingNumber = number,
            Type = request.Type,
            CategoryId = request.Type == StockTakingType.Partial ? request.CategoryId : null,
            Status = StockTakingStatus.InProgress,
            StartedAt = DateTime.UtcNow,
            CreatedByUserId = userId,
            Notes = request.Notes
        };

        _context.StockTakings.Add(stockTaking);
        await _context.SaveChangesAsync();

        // Snapshot: pre-populate items with current system quantities
        var inventoryQuery = _context.BranchInventories
            .Where(bi => bi.BranchId == branchId && bi.TenantId == tenantId)
            .AsQueryable();

        if (request.Type == StockTakingType.Partial && request.CategoryId.HasValue)
        {
            inventoryQuery = inventoryQuery.Where(bi => bi.Product.CategoryId == request.CategoryId.Value);
        }

        var snapshotItems = await inventoryQuery
            .Select(bi => new StockTakingItem
            {
                StockTakingId = stockTaking.Id,
                ProductId = bi.ProductId,
                SystemQuantity = bi.Quantity,
                ActualQuantity = bi.Quantity, // Default to system quantity (no diff initially)
                Reason = null
            })
            .ToListAsync();

        if (snapshotItems.Count > 0)
        {
            _context.StockTakingItems.AddRange(snapshotItems);
            await _context.SaveChangesAsync();
        }

        var dto = new StockTakingDto
        {
            Id = stockTaking.Id,
            StockTakingNumber = stockTaking.StockTakingNumber,
            Type = stockTaking.Type,
            CategoryId = stockTaking.CategoryId,
            Status = stockTaking.Status,
            StartedAt = stockTaking.StartedAt,
            CreatedByUserId = stockTaking.CreatedByUserId,
            Notes = stockTaking.Notes,
            ItemCount = snapshotItems.Count,
            TotalDifference = 0
        };

        return ApiResponse<StockTakingDto>.Ok(dto, "تم إنشاء الجرد بنجاح");
    }

    public async Task<ApiResponse<StockTakingItemDto>> UpsertItemAsync(int stockTakingId, UpsertStockTakingItemRequest request)
    {
        var stockTaking = await _context.StockTakings
            .Include(st => st.Items)
            .FirstOrDefaultAsync(st => st.Id == stockTakingId && st.TenantId == _currentUser.TenantId && st.BranchId == _currentUser.BranchId);

        if (stockTaking == null)
            return ApiResponse<StockTakingItemDto>.Fail(ErrorCodes.STOCK_TAKING_NOT_FOUND, ErrorMessages.Get(ErrorCodes.STOCK_TAKING_NOT_FOUND));

        if (stockTaking.Status != StockTakingStatus.InProgress)
            return ApiResponse<StockTakingItemDto>.Fail(ErrorCodes.STOCK_TAKING_NOT_EDITABLE, ErrorMessages.Get(ErrorCodes.STOCK_TAKING_NOT_EDITABLE));

        if (request.ActualQuantity < 0)
            return ApiResponse<StockTakingItemDto>.Fail(ErrorCodes.INVENTORY_INVALID_QUANTITY, ErrorMessages.Get(ErrorCodes.INVENTORY_INVALID_QUANTITY));

        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == request.ProductId && p.TenantId == _currentUser.TenantId);

        if (product == null)
            return ApiResponse<StockTakingItemDto>.Fail(ErrorCodes.PRODUCT_NOT_FOUND, ErrorMessages.Get(ErrorCodes.PRODUCT_NOT_FOUND));

        // Batch validation
        if (request.BatchId.HasValue)
        {
            var batch = await _context.ProductBatches
                .FirstOrDefaultAsync(b => b.Id == request.BatchId.Value && b.ProductId == request.ProductId && b.BranchId == stockTaking.BranchId && b.TenantId == _currentUser.TenantId);

            if (batch == null)
                return ApiResponse<StockTakingItemDto>.Fail(ErrorCodes.STOCK_TAKING_BATCH_INVALID, ErrorMessages.Get(ErrorCodes.STOCK_TAKING_BATCH_INVALID));

            if (batch.Status != BatchStatus.Active)
                return ApiResponse<StockTakingItemDto>.Fail(ErrorCodes.STOCK_TAKING_BATCH_EXPIRED, ErrorMessages.Get(ErrorCodes.STOCK_TAKING_BATCH_EXPIRED));
        }

        var inventory = await _context.BranchInventories
            .FirstOrDefaultAsync(bi => bi.BranchId == stockTaking.BranchId && bi.ProductId == request.ProductId && bi.TenantId == _currentUser.TenantId);

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
            .FirstOrDefaultAsync(st => st.Id == stockTakingId && st.TenantId == _currentUser.TenantId && st.BranchId == _currentUser.BranchId);

        if (stockTaking == null)
            return ApiResponse<bool>.Fail(ErrorCodes.STOCK_TAKING_NOT_FOUND, ErrorMessages.Get(ErrorCodes.STOCK_TAKING_NOT_FOUND));

        if (stockTaking.Status != StockTakingStatus.InProgress)
            return ApiResponse<bool>.Fail(ErrorCodes.STOCK_TAKING_NOT_EDITABLE, ErrorMessages.Get(ErrorCodes.STOCK_TAKING_NOT_EDITABLE));

        var item = stockTaking.Items.FirstOrDefault(i => i.Id == itemId);
        if (item == null)
            return ApiResponse<bool>.Fail(ErrorCodes.STOCK_TAKING_ITEM_NOT_FOUND, ErrorMessages.Get(ErrorCodes.STOCK_TAKING_ITEM_NOT_FOUND));

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
                .FirstOrDefaultAsync(st => st.Id == id && st.TenantId == _currentUser.TenantId && st.BranchId == _currentUser.BranchId);

            if (stockTaking == null)
                return ApiResponse<StockTakingDto>.Fail(ErrorCodes.STOCK_TAKING_NOT_FOUND, ErrorMessages.Get(ErrorCodes.STOCK_TAKING_NOT_FOUND));

            if (stockTaking.Status != StockTakingStatus.InProgress)
                return ApiResponse<StockTakingDto>.Fail(ErrorCodes.STOCK_TAKING_NOT_EDITABLE, ErrorMessages.Get(ErrorCodes.STOCK_TAKING_NOT_EDITABLE));

            if (request.ApplyAdjustments)
            {
                foreach (var item in stockTaking.Items)
                {
                    var diff = item.ActualQuantity - item.SystemQuantity;
                    if (diff == 0) continue;

                    ProductBatch? batch = null;

                    // Batch validation before applying
                    if (item.BatchId.HasValue)
                    {
                        batch = await _context.ProductBatches
                            .FirstOrDefaultAsync(b => b.Id == item.BatchId.Value && b.ProductId == item.ProductId && b.BranchId == stockTaking.BranchId && b.TenantId == _currentUser.TenantId);

                        if (batch == null)
                            throw new InvalidOperationException($"Batch {item.BatchId} not found for product {item.ProductId}");

                        if (batch.Status != BatchStatus.Active)
                            throw new InvalidOperationException($"Batch {item.BatchId} is not active (status: {batch.Status})");
                    }

                    var inventory = await _context.BranchInventories
                        .FirstOrDefaultAsync(bi => bi.BranchId == stockTaking.BranchId && bi.ProductId == item.ProductId && bi.TenantId == _currentUser.TenantId);

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
                    var balanceAfter = balanceBefore + diff;
                    if (balanceAfter < 0)
                    {
                        await transaction.RollbackAsync();
                        return ApiResponse<StockTakingDto>.Fail(ErrorCodes.INVENTORY_INVALID_QUANTITY, ErrorMessages.Get(ErrorCodes.INVENTORY_INVALID_QUANTITY));
                    }

                    inventory.Quantity = Math.Round(balanceAfter, 4);
                    inventory.LastUpdatedAt = DateTime.UtcNow;

                    if (batch != null)
                    {
                        var batchBalanceAfter = batch.Quantity + diff;
                        if (batchBalanceAfter < 0)
                        {
                            await transaction.RollbackAsync();
                            return ApiResponse<StockTakingDto>.Fail(ErrorCodes.INVENTORY_INVALID_QUANTITY, ErrorMessages.Get(ErrorCodes.INVENTORY_INVALID_QUANTITY));
                        }

                        batch.Quantity = Math.Round(batchBalanceAfter, 4);
                        batch.UpdatedAt = DateTime.UtcNow;
                        if (batch.Quantity <= 0)
                        {
                            batch.Status = BatchStatus.Depleted;
                            batch.StatusUpdatedAt = DateTime.UtcNow;
                        }
                        else if (batch.Status == BatchStatus.Depleted)
                        {
                            batch.Status = BatchStatus.Active;
                            batch.StatusUpdatedAt = DateTime.UtcNow;
                        }
                    }

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
            .FirstOrDefaultAsync(st => st.Id == id && st.TenantId == _currentUser.TenantId && st.BranchId == _currentUser.BranchId);

        if (stockTaking == null)
            return ApiResponse<bool>.Fail(ErrorCodes.STOCK_TAKING_NOT_FOUND, ErrorMessages.Get(ErrorCodes.STOCK_TAKING_NOT_FOUND));

        if (stockTaking.Status != StockTakingStatus.InProgress)
            return ApiResponse<bool>.Fail(ErrorCodes.STOCK_TAKING_NOT_EDITABLE, ErrorMessages.Get(ErrorCodes.STOCK_TAKING_NOT_EDITABLE));

        stockTaking.Status = StockTakingStatus.Cancelled;
        stockTaking.CompletedAt = DateTime.UtcNow;
        stockTaking.CompletedByUserId = _currentUser.UserId;

        await _context.SaveChangesAsync();
        return ApiResponse<bool>.Ok(true, "تم إلغاء الجرد بنجاح");
    }

    public async Task<StockTakingDto?> GetLatestCompletedAsync()
    {
        var latest = await _context.StockTakings
            .AsNoTracking()
            .Include(st => st.Category)
            .Include(st => st.CreatedByUser)
            .Include(st => st.CompletedByUser)
            .Include(st => st.Items)
            .Where(st => st.TenantId == _currentUser.TenantId
                      && st.BranchId == _currentUser.BranchId
                      && st.Status == StockTakingStatus.Completed)
            .OrderByDescending(st => st.CompletedAt)
            .FirstOrDefaultAsync();

        return latest == null ? null : MapToDto(latest);
    }

    private static StockTakingDto MapToDto(StockTaking stockTaking, bool includeItems = false)
    {
        var items = stockTaking.Items ?? new List<StockTakingItem>();

        return new StockTakingDto
        {
            Id = stockTaking.Id,
            StockTakingNumber = stockTaking.StockTakingNumber,
            Type = stockTaking.Type,
            CategoryId = stockTaking.CategoryId,
            CategoryName = stockTaking.Category?.Name,
            Status = stockTaking.Status,
            StartedAt = stockTaking.StartedAt,
            CompletedAt = stockTaking.CompletedAt,
            CreatedByUserId = stockTaking.CreatedByUserId,
            CreatedByUserName = stockTaking.CreatedByUser?.Name,
            CompletedByUserId = stockTaking.CompletedByUserId,
            CompletedByUserName = stockTaking.CompletedByUser?.Name,
            Notes = stockTaking.Notes,
            ItemCount = items.Count,
            TotalDifference = items.Sum(i => i.ActualQuantity - i.SystemQuantity),
            Items = includeItems
                ? items.Select(i => new StockTakingItemDto
                {
                    Id = i.Id,
                    ProductId = i.ProductId,
                    ProductName = i.Product?.Name ?? string.Empty,
                    ProductSku = i.Product?.Sku,
                    SystemQuantity = i.SystemQuantity,
                    ActualQuantity = i.ActualQuantity,
                    Difference = i.ActualQuantity - i.SystemQuantity,
                    Reason = i.Reason,
                    BatchId = i.BatchId,
                    BatchNumber = i.Batch?.BatchNumber
                }).ToList()
                : new List<StockTakingItemDto>()
        };
    }
}
